using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Ludots.Core.Modding;
using Ludots.Core.Presentation.AdapterSync;
using Ludots.Core.Presentation.Assets;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Rendering;
using Raylib_cs;
using Rl = Raylib_cs.Raylib;

namespace Ludots.Client.Raylib.Rendering
{
    public enum RaylibPrimitiveRenderMode : byte
    {
        Immediate = 0,
        Instanced = 1
    }

    public sealed unsafe class RaylibPrimitiveRenderer : IDisposable
    {
        private readonly RaylibPrimitiveRenderMode _mode;
        private readonly IVirtualFileSystem? _vfs;
        private readonly string? _diagnosticPath;
        private const int MaxPrefabDepth = 6;

        private bool _initialized;
        private Mesh _cubeMesh;
        private Mesh _sphereMesh;
        private Shader _shader;
        private Material _material;
        private int _locColDiffuse;
        private int _locTint;

        private readonly List<Batch> _cubeBatches = new List<Batch>(16);
        private readonly List<Batch> _sphereBatches = new List<Batch>(16);
        private readonly StaticMeshAdapterSyncPlanner _persistentStaticLaneSync = new StaticMeshAdapterSyncPlanner();

        private readonly Dictionary<int, CachedModel> _modelCache = new Dictionary<int, CachedModel>();
        private readonly Dictionary<int, CachedTexture> _textureCache = new Dictionary<int, CachedTexture>();
        private readonly HashSet<int> _loggedTextureDiagnostics = new HashSet<int>();
        private readonly HashSet<int> _loggedBillboardDrawDiagnostics = new HashSet<int>();

        public int LastInstancedInstances { get; private set; }
        public int LastInstancedBatches { get; private set; }
        public int LastPersistentCreates { get; private set; }
        public int LastPersistentUpdates { get; private set; }
        public int LastPersistentRemoves { get; private set; }

        public RaylibPrimitiveRenderer(
            RaylibPrimitiveRenderMode mode = RaylibPrimitiveRenderMode.Immediate,
            IVirtualFileSystem? vfs = null)
        {
            _mode = mode;
            _vfs = vfs;
            _diagnosticPath = Environment.GetEnvironmentVariable("LUDOTS_RAYLIB_DIAGNOSTIC_PATH");
        }

        public void Draw(PrimitiveDrawBuffer draw, Camera3D camera, MeshAssetRegistry meshes, float scaleMul = 1f)
        {
            Draw(draw, camera, snapshot: null, meshes, scaleMul);
        }

        public void Draw(PrimitiveDrawBuffer draw, Camera3D camera, PrimitiveDrawBuffer? snapshot, MeshAssetRegistry meshes, float scaleMul = 1f)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            if (meshes == null) throw new ArgumentNullException(nameof(meshes));

            LastInstancedInstances = 0;
            LastInstancedBatches = 0;
            LastPersistentCreates = 0;
            LastPersistentUpdates = 0;
            LastPersistentRemoves = 0;

            var span = draw.GetSpan();
            bool usePersistentStaticLanes = snapshot != null;
            if (usePersistentStaticLanes)
            {
                _persistentStaticLaneSync.Sync(snapshot);
                LastPersistentCreates = _persistentStaticLaneSync.LastCreateCount;
                LastPersistentUpdates = _persistentStaticLaneSync.LastUpdateCount;
                LastPersistentRemoves = _persistentStaticLaneSync.LastRemoveCount;
                DrawPersistentStaticLanes(camera, meshes, scaleMul);
                DrawImmediateWithDescriptors(span, camera, meshes, scaleMul, persistentStaticLanesActive: true);
                return;
            }

            if (_mode == RaylibPrimitiveRenderMode.Instanced)
            {
                DrawHybridInstanced(span, camera, meshes, scaleMul);
                return;
            }

            DrawImmediateWithDescriptors(span, camera, meshes, scaleMul, persistentStaticLanesActive: false);
        }

        private void DrawPersistentStaticLanes(Camera3D camera, MeshAssetRegistry meshes, float scaleMul)
        {
            foreach (var pair in _persistentStaticLaneSync.ActiveBindings)
            {
                var binding = pair.Value;
                var item = binding.Item;
                if (!binding.IsVisible)
                {
                    continue;
                }

                DrawAssetRecursive(
                    item.MeshAssetId,
                    item.Position,
                    item.Scale * scaleMul,
                    item.Color,
                    camera,
                    meshes,
                    0);
            }
        }

        private void DrawImmediateWithDescriptors(ReadOnlySpan<PrimitiveDrawItem> span, Camera3D camera, MeshAssetRegistry meshes, float scaleMul, bool persistentStaticLanesActive)
        {
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var item = ref span[i];
                if (persistentStaticLanesActive &&
                    item.StableId > 0 &&
                    StaticMeshLaneKey.Supports(item.RenderPath))
                {
                    continue;
                }

                DrawAssetRecursive(
                    item.MeshAssetId, item.Position,
                    item.Scale * scaleMul, item.Color,
                    camera,
                    meshes, 0);
            }
        }

        private void DrawHybridInstanced(ReadOnlySpan<PrimitiveDrawItem> span, Camera3D camera, MeshAssetRegistry meshes, float scaleMul)
        {
            EnsureInitialized();

            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var item = ref span[i];
                SubmitAssetRecursive(
                    item.MeshAssetId,
                    item.Position,
                    item.Scale * scaleMul,
                    item.Color,
                    camera,
                    meshes,
                    depth: 0);
            }

            FlushInstancedBatches();
        }

        private void SubmitAssetRecursive(int meshAssetId, Vector3 position, Vector3 scale, Vector4 color, Camera3D camera, MeshAssetRegistry meshes, int depth)
        {
            if (depth > MaxPrefabDepth) return;
            if (!meshes.TryGetDescriptor(meshAssetId, out var desc)) return;

            switch (desc.Type)
            {
                case MeshAssetType.Primitive:
                    SubmitPrimitive(desc.PrimitiveKind, position, scale, color);
                    break;

                case MeshAssetType.Model:
                    DrawModel(meshAssetId, desc, position, scale, color);
                    break;

                case MeshAssetType.Billboard:
                    DrawBillboard(meshAssetId, desc, position, scale, color, camera);
                    break;

                case MeshAssetType.Prefab:
                    if (desc.PrefabParts != null)
                    {
                        for (int p = 0; p < desc.PrefabParts.Length; p++)
                        {
                            ref var part = ref desc.PrefabParts[p];
                            var childPos = position + part.LocalPosition * scale;
                            var childScale = scale * part.LocalScale;
                            var childColor = new Vector4(
                                color.X * part.ColorTint.X,
                                color.Y * part.ColorTint.Y,
                                color.Z * part.ColorTint.Z,
                                color.W * part.ColorTint.W);
                            SubmitAssetRecursive(part.MeshAssetId, childPos, childScale, childColor, camera, meshes, depth + 1);
                        }
                    }
                    break;
            }
        }

        private void SubmitPrimitive(PrimitiveMeshKind kind, Vector3 position, Vector3 scale, Vector4 color)
        {
            uint key = PackRgba(color);
            var matrix = RaylibMatrix.FromScaleTranslation(position.X, position.Y, position.Z, scale.X, scale.Y, scale.Z);

            if (kind == PrimitiveMeshKind.Cube)
            {
                AddInstance(_cubeBatches, key, matrix);
                return;
            }

            if (kind == PrimitiveMeshKind.Sphere)
            {
                AddInstance(_sphereBatches, key, matrix);
                return;
            }

            DrawPrimitive(kind, position, scale, color);
        }

        private void DrawAssetRecursive(int meshAssetId, Vector3 position, Vector3 scale, Vector4 color, Camera3D camera, MeshAssetRegistry meshes, int depth)
        {
            if (depth > MaxPrefabDepth) return;
            if (!meshes.TryGetDescriptor(meshAssetId, out var desc)) return;

            switch (desc.Type)
            {
                case MeshAssetType.Primitive:
                    DrawPrimitive(desc.PrimitiveKind, position, scale, color);
                    break;

                case MeshAssetType.Model:
                    DrawModel(meshAssetId, desc, position, scale, color);
                    break;

                case MeshAssetType.Billboard:
                    DrawBillboard(meshAssetId, desc, position, scale, color, camera);
                    break;

                case MeshAssetType.Prefab:
                    if (desc.PrefabParts != null)
                    {
                        for (int p = 0; p < desc.PrefabParts.Length; p++)
                        {
                            ref var part = ref desc.PrefabParts[p];
                            var childPos = position + part.LocalPosition * scale;
                            var childScale = scale * part.LocalScale;
                            var childColor = new Vector4(
                                color.X * part.ColorTint.X,
                                color.Y * part.ColorTint.Y,
                                color.Z * part.ColorTint.Z,
                                color.W * part.ColorTint.W);
                            DrawAssetRecursive(part.MeshAssetId, childPos, childScale, childColor, camera, meshes, depth + 1);
                        }
                    }
                    break;
            }
        }

        private void DrawPrimitive(PrimitiveMeshKind kind, Vector3 position, Vector3 scale, Vector4 color)
        {
            var c = ToRaylibColor(color);
            if (kind == PrimitiveMeshKind.Cube)
            {
                Rl.DrawCube(position, scale.X, scale.Y, scale.Z, c);
            }
            else if (kind == PrimitiveMeshKind.Sphere)
            {
                float r = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)) * 0.5f;
                Rl.DrawSphere(position, r, c);
            }
        }

        private void DrawModel(int meshAssetId, in MeshAssetDescriptor desc, Vector3 position, Vector3 scale, Vector4 color)
        {
            if (!TryGetOrLoadModel(meshAssetId, desc, out var cached))
            {
                DrawMissingModelMarker(position, scale);
                return;
            }

            var tint = ToRaylibColor(color);
            var model = cached.Model;
            Rl.DrawModelEx(model, position, Vector3.UnitY, 0f, scale, tint);
        }

        private void DrawBillboard(int meshAssetId, in MeshAssetDescriptor desc, Vector3 position, Vector3 scale, Vector4 color, Camera3D camera)
        {
            if (!TryGetOrLoadTexture(meshAssetId, desc, out var cached))
            {
                DrawMissingModelMarker(position, scale);
                return;
            }

            float height = MathF.Max(scale.Y, 0.05f);
            float width = height * cached.AspectRatio;
            var billboardPosition = new Vector3(position.X, position.Y + height * 0.5f, position.Z);
            var source = new Rectangle(0f, 0f, cached.Texture.width, cached.Texture.height);

            // Billboard art ships pre-colored, so preserve only caller alpha.
            byte alpha = Clamp01ToByte(color.W);
            var tint = new Color(255, 255, 255, alpha);
            LogBillboardDrawDiagnostic(
                meshAssetId,
                $"billboard-draw pos=({billboardPosition.X:F2},{billboardPosition.Y:F2},{billboardPosition.Z:F2}) scale=({scale.X:F2},{scale.Y:F2},{scale.Z:F2}) size=({width:F2}x{height:F2}) alpha={alpha} cameraPos=({camera.position.X:F2},{camera.position.Y:F2},{camera.position.Z:F2}) cameraTarget=({camera.target.X:F2},{camera.target.Y:F2},{camera.target.Z:F2})");
            Rl.DrawBillboardRec(camera, cached.Texture, source, billboardPosition, new Vector2(width, height), tint);
        }

        private bool TryGetOrLoadModel(int meshAssetId, in MeshAssetDescriptor desc, out CachedModel cached)
        {
            if (_modelCache.TryGetValue(meshAssetId, out cached))
                return cached.Loaded;

            cached = new CachedModel { Loaded = false };

            if (_vfs == null || desc.SourceUris == null || desc.SourceUris.Length == 0)
            {
                _modelCache[meshAssetId] = cached;
                return false;
            }

            for (int u = 0; u < desc.SourceUris.Length; u++)
            {
                string uri = desc.SourceUris[u];
                if (string.IsNullOrWhiteSpace(uri)) continue;

                if (!_vfs.TryResolveFullPath(uri, out string fullPath)) continue;
                if (!File.Exists(fullPath)) continue;

                var model = Rl.LoadModel(fullPath);
                if (model.meshCount > 0)
                {
                    cached = new CachedModel { Model = model, Loaded = true };
                    _modelCache[meshAssetId] = cached;
                    return true;
                }

                Rl.UnloadModel(model);
            }

            _modelCache[meshAssetId] = cached;
            return false;
        }

        private bool TryGetOrLoadTexture(int meshAssetId, in MeshAssetDescriptor desc, out CachedTexture cached)
        {
            if (_textureCache.TryGetValue(meshAssetId, out cached))
                return cached.Loaded;

            cached = new CachedTexture { Loaded = false, AspectRatio = 1f };

            if (_vfs == null || desc.SourceUris == null || desc.SourceUris.Length == 0)
            {
                LogTextureDiagnostic(meshAssetId, $"texture-load skipped; vfsMissing={_vfs == null}; uriCount={desc.SourceUris?.Length ?? 0}");
                _textureCache[meshAssetId] = cached;
                return false;
            }

            for (int u = 0; u < desc.SourceUris.Length; u++)
            {
                string uri = desc.SourceUris[u];
                if (string.IsNullOrWhiteSpace(uri)) continue;

                if (!_vfs.TryResolveFullPath(uri, out string fullPath))
                {
                    LogTextureDiagnostic(meshAssetId, $"texture-resolve failed; uri={uri}");
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    LogTextureDiagnostic(meshAssetId, $"texture-file missing; uri={uri}; fullPath={fullPath}");
                    continue;
                }

                var texture = Rl.LoadTexture(fullPath);
                if (texture.id != 0 && texture.width > 0 && texture.height > 0)
                {
                    cached = new CachedTexture
                    {
                        Texture = texture,
                        Loaded = true,
                        AspectRatio = texture.height > 0 ? (float)texture.width / texture.height : 1f,
                    };
                    _textureCache[meshAssetId] = cached;
                    LogTextureDiagnostic(meshAssetId, $"texture-load success; uri={uri}; fullPath={fullPath}; size={texture.width}x{texture.height}");
                    return true;
                }

                LogTextureDiagnostic(meshAssetId, $"texture-load failed; uri={uri}; fullPath={fullPath}; textureId={texture.id}; size={texture.width}x{texture.height}");

                if (texture.id != 0)
                    Rl.UnloadTexture(texture);
            }

            _textureCache[meshAssetId] = cached;
            return false;
        }

        private void LogTextureDiagnostic(int meshAssetId, string message)
        {
            if (string.IsNullOrWhiteSpace(_diagnosticPath))
                return;

            if (!_loggedTextureDiagnostics.Add(meshAssetId))
                return;

            string fullPath = Path.GetFullPath(_diagnosticPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(fullPath, $"[{DateTime.UtcNow:O}] meshAssetId={meshAssetId} {message}{Environment.NewLine}");
        }

        private void LogBillboardDrawDiagnostic(int meshAssetId, string message)
        {
            if (string.IsNullOrWhiteSpace(_diagnosticPath))
                return;

            if (!_loggedBillboardDrawDiagnostics.Add(meshAssetId))
                return;

            string fullPath = Path.GetFullPath(_diagnosticPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(fullPath, $"[{DateTime.UtcNow:O}] meshAssetId={meshAssetId} {message}{Environment.NewLine}");
        }

        private static void DrawMissingModelMarker(Vector3 position, Vector3 scale)
        {
            float s = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)) * 0.3f;
            if (s < 0.05f) s = 0.3f;
            Rl.DrawCube(position, s, s, s, new Color(255, 0, 255, 255));
        }

        // ── Instanced rendering (unchanged from original) ──

        public void DrawInstanced(PrimitiveDrawBuffer draw, MeshAssetRegistry meshes)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            if (meshes == null) throw new ArgumentNullException(nameof(meshes));

            LastInstancedInstances = 0;
            LastInstancedBatches = 0;

            EnsureInitialized();

            var span = draw.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                ref readonly var item = ref span[i];
                if (!meshes.TryGetPrimitiveKind(item.MeshAssetId, out var kind)) continue;

                SubmitPrimitive(kind, item.Position, item.Scale, item.Color);
            }

            FlushInstancedBatches();
        }

        private void AddInstance(List<Batch> batches, uint colorKey, in RaylibMatrix matrix)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                var b = batches[i];
                if (b.ColorKey != colorKey) continue;

                b.Add(matrix);
                batches[i] = b;
                return;
            }

            var nb = new Batch(colorKey);
            nb.Add(matrix);
            batches.Add(nb);
        }

        private void FlushInstancedBatches()
        {
            int totalInstances = 0;
            int batches = 0;

            FlushMeshBatches(_cubeBatches, ref totalInstances, ref batches, ref _cubeMesh);
            FlushMeshBatches(_sphereBatches, ref totalInstances, ref batches, ref _sphereMesh);

            LastInstancedInstances = totalInstances;
            LastInstancedBatches = batches;
        }

        private void FlushMeshBatches(List<Batch> batches, ref int totalInstances, ref int batchCount, ref Mesh mesh)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                var b = batches[i];
                if (b.Count == 0) continue;

                SetTintUniform(b.ColorKey);

                fixed (RaylibMatrix* p = b.Transforms)
                {
                    Rl.DrawMeshInstanced(mesh, _material, p, b.Count);
                }

                totalInstances += b.Count;
                batchCount++;

                b.Count = 0;
                batches[i] = b;
            }
        }

        private void SetTintUniform(uint colorKey)
        {
            if (_locTint < 0) return;

            float r = (colorKey & 0xFF) / 255f;
            float g = ((colorKey >> 8) & 0xFF) / 255f;
            float b = ((colorKey >> 16) & 0xFF) / 255f;
            float a = ((colorKey >> 24) & 0xFF) / 255f;
            var cd = new Vector4(r, g, b, a);
            Rl.SetShaderValue(_shader, _locTint, &cd, (int)Rl.ShaderUniformDataType.SHADER_UNIFORM_VEC4);
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _cubeMesh = Rl.GenMeshCube(1f, 1f, 1f);
            if (_cubeMesh.colors == null)
            {
                int bytes = _cubeMesh.vertexCount * 4;
                _cubeMesh.colors = (byte*)Rl.MemAlloc(bytes);
                for (int i = 0; i < bytes; i++) _cubeMesh.colors[i] = 255;
            }
            Rl.UploadMesh(ref _cubeMesh, false);

            _sphereMesh = Rl.GenMeshSphere(0.5f, 8, 8);
            if (_sphereMesh.colors == null)
            {
                int bytes = _sphereMesh.vertexCount * 4;
                _sphereMesh.colors = (byte*)Rl.MemAlloc(bytes);
                for (int i = 0; i < bytes; i++) _sphereMesh.colors[i] = 255;
            }
            Rl.UploadMesh(ref _sphereMesh, false);

            string baseDir = AppContext.BaseDirectory;
            _shader = Rl.LoadShader(Path.Combine(baseDir, "instancing.vs"), Path.Combine(baseDir, "instancing.fs"));
            if (_shader.id == 0) throw new InvalidOperationException("Failed to load instancing shader (shader.id == 0).");

            _material = Rl.LoadMaterialDefault();
            _material.shader = _shader;

            _locColDiffuse = Rl.GetShaderLocation(_shader, "colDiffuse");
            _locTint = Rl.GetShaderLocation(_shader, "tint");
            int locMvp = Rl.GetShaderLocation(_shader, "mvp");
            int locInstance = Rl.GetShaderLocationAttrib(_shader, "instanceTransform");

            _shader.locs[(int)Rl.ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] = locMvp;
            _shader.locs[(int)Rl.ShaderLocationIndex.SHADER_LOC_MATRIX_MODEL] = locInstance;
            _shader.locs[(int)Rl.ShaderLocationIndex.SHADER_LOC_COLOR_DIFFUSE] = _locColDiffuse;

            if (locMvp < 0) throw new InvalidOperationException("Shader uniform 'mvp' not found.");
            if (locInstance < 0) throw new InvalidOperationException("Shader attrib 'instanceTransform' not found.");
            if (_locTint < 0) throw new InvalidOperationException("Shader uniform 'tint' not found.");

            _initialized = true;
        }

        private static uint PackRgba(in Vector4 c)
        {
            uint r = Clamp01ToByte(c.X);
            uint g = Clamp01ToByte(c.Y);
            uint b = Clamp01ToByte(c.Z);
            uint a = Clamp01ToByte(c.W);
            return r | (g << 8) | (b << 16) | (a << 24);
        }

        private static Color ToRaylibColor(in Vector4 c) => RaylibColorUtil.ToRaylibColor(in c);

        private static byte Clamp01ToByte(float v) => RaylibColorUtil.Clamp01ToByte(v);

        public void Dispose()
        {
            foreach (var kvp in _modelCache)
            {
                if (kvp.Value.Loaded)
                    Rl.UnloadModel(kvp.Value.Model);
            }
            _modelCache.Clear();

            foreach (var kvp in _textureCache)
            {
                if (kvp.Value.Loaded)
                    Rl.UnloadTexture(kvp.Value.Texture);
            }
            _textureCache.Clear();

            if (!_initialized) return;

            if (_cubeMesh.vertexCount > 0) Rl.UnloadMesh(_cubeMesh);
            if (_sphereMesh.vertexCount > 0) Rl.UnloadMesh(_sphereMesh);
            Rl.UnloadMaterial(_material);
            Rl.UnloadShader(_shader);
        }

        private struct CachedModel
        {
            public Model Model;
            public bool Loaded;
        }

        private struct CachedTexture
        {
            public Texture2D Texture;
            public bool Loaded;
            public float AspectRatio;
        }

        private struct Batch
        {
            public readonly uint ColorKey;
            public RaylibMatrix[] Transforms;
            public int Count;

            public Batch(uint colorKey, int initialCapacity = 256)
            {
                ColorKey = colorKey;
                Transforms = new RaylibMatrix[Math.Max(4, initialCapacity)];
                Count = 0;
            }

            public void Add(in RaylibMatrix matrix)
            {
                if (Count >= Transforms.Length)
                {
                    Array.Resize(ref Transforms, Transforms.Length * 2);
                }
                Transforms[Count++] = matrix;
            }
        }
    }
}
