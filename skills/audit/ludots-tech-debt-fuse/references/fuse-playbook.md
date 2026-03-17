# Fuse Playbook

## Fuse Modes

1. Hard stop
- Abort path immediately.
- Use for P0/P1 correctness and unsafe state risks.

2. Explicit degrade
- Continue with reduced capability under explicit feature flag.
- Must emit clear warning and metric counter.

3. Isolation
- Route around faulty component to bounded fallback module.
- Only valid when fallback is tested and behavior is documented.

## Mandatory Observability

- emit fuse mode
- emit debt id
- emit impacted feature/scenario id
- emit branch reason code

## Forbidden

- silent fallback
- hidden no-op in critical gameplay path
- introducing parallel runtime stack as a temporary fix

