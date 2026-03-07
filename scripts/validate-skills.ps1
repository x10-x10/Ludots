[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Add-Finding {
    param(
        [System.Collections.Generic.List[object]]$Collection,
        [string]$Rule,
        [string]$Source,
        [string]$Detail
    )

    $Collection.Add([PSCustomObject]@{
        Rule = $Rule
        Source = $Source
        Detail = $Detail
    }) | Out-Null
}

function Get-RelativePath {
    param(
        [string]$RepoRoot,
        [string]$FullPath
    )

    return $FullPath.Replace($RepoRoot + [System.IO.Path]::DirectorySeparatorChar, '').Replace([System.IO.Path]::DirectorySeparatorChar, '/')
}

function Get-FrontmatterBlock {
    param([string]$Path)

    $content = Get-Content -Raw -Encoding UTF8 $Path
    $match = [regex]::Match($content, '^(---\r?\n)(.*?)(\r?\n---)', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[2].Value
}

function Get-FrontmatterValue {
    param(
        [string]$Frontmatter,
        [string]$Key
    )

    if ([string]::IsNullOrWhiteSpace($Frontmatter)) {
        return $null
    }

    $match = [regex]::Match($Frontmatter, "(?m)^$([regex]::Escape($Key)):\s*(.+?)\s*$")
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[1].Value.Trim().Trim('"')
}

function Get-MarkdownLinks {
    param([string]$Content)

    return [regex]::Matches($Content, '\[[^\]]+\]\(([^)]+)\)')
}

function Resolve-RepoTarget {
    param(
        [string]$SourceFile,
        [string]$Target
    )

    if ([string]::IsNullOrWhiteSpace($Target)) {
        return $null
    }

    $normalizedTarget = $Target.Split('#')[0].Split('?')[0]
    if ([string]::IsNullOrWhiteSpace($normalizedTarget)) {
        return $null
    }

    if ($normalizedTarget -match '^(https?:|mailto:|#)') {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($normalizedTarget)) {
        return $normalizedTarget
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $SourceFile) $normalizedTarget))
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '..'))
$skillsRoot = Join-Path $repoRoot 'skills'
$registryPath = Join-Path $skillsRoot 'registry.json'
$findings = New-Object 'System.Collections.Generic.List[object]'

foreach ($required in @(
    'skills/README.md',
    'skills/registry.json',
    'skills/contracts/hook.schema.json',
    'skills/contracts/evidence-manifest.schema.json',
    'skills/contracts/review-result.schema.json'
)) {
    $fullPath = Join-Path $repoRoot $required
    if (-not (Test-Path $fullPath)) {
        Add-Finding -Collection $findings -Rule 'missing-core-file' -Source $required -Detail 'required shared skill file is missing'
    }
}

if (-not (Test-Path $registryPath)) {
    Write-Host 'Skill validation failed.' -ForegroundColor Red
    $findings | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

try {
    $registry = Get-Content -Raw -Encoding UTF8 $registryPath | ConvertFrom-Json
} catch {
    Add-Finding -Collection $findings -Rule 'invalid-registry-json' -Source 'skills/registry.json' -Detail $_.Exception.Message
}

foreach ($contractPath in @(
    'skills/contracts/hook.schema.json',
    'skills/contracts/evidence-manifest.schema.json',
    'skills/contracts/review-result.schema.json'
)) {
    try {
        Get-Content -Raw -Encoding UTF8 (Join-Path $repoRoot $contractPath) | ConvertFrom-Json | Out-Null
    } catch {
        Add-Finding -Collection $findings -Rule 'invalid-contract-json' -Source $contractPath -Detail $_.Exception.Message
    }
}

if ($null -eq $registry) {
    Write-Host 'Skill validation failed.' -ForegroundColor Red
    $findings | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

$layerMap = @{}
foreach ($layer in @($registry.layers)) {
    $layerMap[$layer.id] = $layer.path
    $layerPath = Join-Path $repoRoot $layer.path
    if (-not (Test-Path $layerPath)) {
        Add-Finding -Collection $findings -Rule 'missing-layer-path' -Source $layer.path -Detail 'layer path declared in registry does not exist'
    }
}

$hookMap = @{}
foreach ($hook in @($registry.hooks)) {
    if ($hookMap.ContainsKey($hook.name)) {
        Add-Finding -Collection $findings -Rule 'duplicate-hook' -Source 'skills/registry.json' -Detail "duplicate hook '$($hook.name)'"
        continue
    }

    $hookMap[$hook.name] = $hook

    $schemaPath = Join-Path $repoRoot $hook.schema
    if (-not (Test-Path $schemaPath)) {
        Add-Finding -Collection $findings -Rule 'missing-hook-schema' -Source 'skills/registry.json' -Detail "schema '$($hook.schema)' for hook '$($hook.name)' does not exist"
    }
}

$skillNameMap = @{}
foreach ($skill in @($registry.skills)) {
    if ($skillNameMap.ContainsKey($skill.name)) {
        Add-Finding -Collection $findings -Rule 'duplicate-skill-name' -Source 'skills/registry.json' -Detail "duplicate skill '$($skill.name)'"
        continue
    }

    $skillNameMap[$skill.name] = $skill
}

$discoveredSkillDirs = Get-ChildItem -Path $skillsRoot -Recurse -Directory | Where-Object {
    Test-Path (Join-Path $_.FullName 'SKILL.md')
}

foreach ($dir in $discoveredSkillDirs) {
    $relativePath = Get-RelativePath -RepoRoot $repoRoot -FullPath $dir.FullName
    if (-not (@($registry.skills.path) -contains $relativePath)) {
        Add-Finding -Collection $findings -Rule 'unregistered-skill' -Source $relativePath -Detail 'skill directory exists on disk but is missing from registry'
    }

    if (Test-Path (Join-Path $dir.FullName 'README.md')) {
        Add-Finding -Collection $findings -Rule 'unexpected-readme' -Source $relativePath -Detail 'leaf skill directory should not contain README.md'
    }
}

foreach ($skill in @($registry.skills)) {
    $skillPath = Join-Path $repoRoot $skill.path
    $skillSource = $skill.path

    if (-not (Test-Path $skillPath)) {
        Add-Finding -Collection $findings -Rule 'missing-skill-path' -Source $skillSource -Detail 'skill path declared in registry does not exist'
        continue
    }

    if (-not $layerMap.ContainsKey($skill.layer)) {
        Add-Finding -Collection $findings -Rule 'unknown-layer' -Source $skillSource -Detail "layer '$($skill.layer)' is not declared in registry"
    } elseif (-not $skill.path.StartsWith($layerMap[$skill.layer] + '/')) {
        Add-Finding -Collection $findings -Rule 'layer-path-mismatch' -Source $skillSource -Detail "skill path is not under declared layer '$($skill.layer)'"
    }

    foreach ($requiredRelative in @(
        (Join-Path $skill.path 'SKILL.md').Replace('\\', '/'),
        $skill.agents.openai,
        $skill.agents.claude
    )) {
        $requiredFull = Join-Path $repoRoot $requiredRelative
        if (-not (Test-Path $requiredFull)) {
            Add-Finding -Collection $findings -Rule 'missing-skill-file' -Source $skillSource -Detail "required file '$requiredRelative' is missing"
        }
    }

    foreach ($reference in @($skill.references)) {
        if (-not (Test-Path (Join-Path $repoRoot $reference))) {
            Add-Finding -Collection $findings -Rule 'missing-reference-file' -Source $skillSource -Detail "reference '$reference' is missing"
        }
    }

    $skillMarkdownPath = Join-Path $skillPath 'SKILL.md'
    if (Test-Path $skillMarkdownPath) {
        $frontmatter = Get-FrontmatterBlock -Path $skillMarkdownPath
        if ($null -eq $frontmatter) {
            Add-Finding -Collection $findings -Rule 'missing-frontmatter' -Source $skillSource -Detail 'SKILL.md is missing YAML frontmatter'
        } else {
            $frontmatterName = Get-FrontmatterValue -Frontmatter $frontmatter -Key 'name'
            $frontmatterDescription = Get-FrontmatterValue -Frontmatter $frontmatter -Key 'description'
            if ($frontmatterName -ne $skill.name) {
                Add-Finding -Collection $findings -Rule 'frontmatter-name-mismatch' -Source $skillSource -Detail "frontmatter name '$frontmatterName' does not match registry name '$($skill.name)'"
            }
            if ([string]::IsNullOrWhiteSpace($frontmatterDescription)) {
                Add-Finding -Collection $findings -Rule 'missing-frontmatter-description' -Source $skillSource -Detail 'frontmatter description is empty'
            }
        }

        $skillContent = Get-Content -Raw -Encoding UTF8 $skillMarkdownPath
        foreach ($match in Get-MarkdownLinks -Content $skillContent) {
            $target = $match.Groups[1].Value.Trim()
            $resolved = Resolve-RepoTarget -SourceFile $skillMarkdownPath -Target $target
            if ($null -eq $resolved) {
                continue
            }
            if (-not (Test-Path $resolved)) {
                Add-Finding -Collection $findings -Rule 'missing-markdown-target' -Source $skillSource -Detail "SKILL.md link target not found: '$target'"
            }
        }
    }

    $openaiPath = Join-Path $repoRoot $skill.agents.openai
    if (Test-Path $openaiPath) {
        $openaiContent = Get-Content -Raw -Encoding UTF8 $openaiPath
        foreach ($field in @('display_name', 'short_description', 'default_prompt')) {
            $pattern = "(?m)^\s*" + [regex]::Escape($field) + ":\s*.+$"
            if ($openaiContent -notmatch $pattern) {
                Add-Finding -Collection $findings -Rule 'missing-openai-field' -Source $skillSource -Detail "agents/openai.yaml is missing field '$field'"
            }
        }
    }

    $claudePath = Join-Path $repoRoot $skill.agents.claude
    if (Test-Path $claudePath) {
        $claudeContent = Get-Content -Raw -Encoding UTF8 $claudePath
        if ([string]::IsNullOrWhiteSpace($claudeContent)) {
            Add-Finding -Collection $findings -Rule 'empty-claude-file' -Source $skillSource -Detail 'agents/claude.md is empty'
        }
        foreach ($match in Get-MarkdownLinks -Content $claudeContent) {
            $target = $match.Groups[1].Value.Trim()
            $resolved = Resolve-RepoTarget -SourceFile $claudePath -Target $target
            if ($null -eq $resolved) {
                continue
            }
            if (-not (Test-Path $resolved)) {
                Add-Finding -Collection $findings -Rule 'missing-claude-link-target' -Source $skillSource -Detail "agents/claude.md link target not found: '$target'"
            }
        }
    }

    foreach ($hookName in @($skill.consumes_hooks)) {
        if (-not $hookMap.ContainsKey($hookName)) {
            Add-Finding -Collection $findings -Rule 'unknown-consumed-hook' -Source $skillSource -Detail "consumed hook '$hookName' is not declared"
        } elseif (-not (@($hookMap[$hookName].consumers) -contains $skill.name)) {
            Add-Finding -Collection $findings -Rule 'hook-consumer-mismatch' -Source $skillSource -Detail "registry hook '$hookName' does not list '$($skill.name)' as consumer"
        }
    }

    foreach ($hookName in @($skill.produces_hooks)) {
        if (-not $hookMap.ContainsKey($hookName)) {
            Add-Finding -Collection $findings -Rule 'unknown-produced-hook' -Source $skillSource -Detail "produced hook '$hookName' is not declared"
        } elseif (-not (@($hookMap[$hookName].producers) -contains $skill.name)) {
            Add-Finding -Collection $findings -Rule 'hook-producer-mismatch' -Source $skillSource -Detail "registry hook '$hookName' does not list '$($skill.name)' as producer"
        }
    }
}

foreach ($hook in @($registry.hooks)) {
    foreach ($producer in @($hook.producers)) {
        if (-not $skillNameMap.ContainsKey($producer)) {
            Add-Finding -Collection $findings -Rule 'unknown-hook-producer' -Source 'skills/registry.json' -Detail "hook '$($hook.name)' references unknown producer '$producer'"
        }
    }

    foreach ($consumer in @($hook.consumers)) {
        if (-not $skillNameMap.ContainsKey($consumer)) {
            Add-Finding -Collection $findings -Rule 'unknown-hook-consumer' -Source 'skills/registry.json' -Detail "hook '$($hook.name)' references unknown consumer '$consumer'"
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host 'Skill validation failed.' -ForegroundColor Red
    $findings | Sort-Object Rule, Source, Detail | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

Write-Host 'Skill validation passed.' -ForegroundColor Green



