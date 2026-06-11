# Registry client tests

Two kinds of tests live here:

| File | Network? | Runs in CI? |
|------|----------|-------------|
| `*RegistryClientTests.cs` (characterization, via `FakeHttpMessageHandler`) | No (mocked) | Yes — part of the normal suite |
| `RegistryClientsIntegrationTests.cs` | Yes — hits real public registries | **No** — skipped unless opted in |

The integration tests are gated by `[IntegrationFact]`, which skips them unless the
environment variable `RUN_REGISTRY_INTEGRATION=1` is set. So a normal `dotnet test`
reports them as *skipped* and CI never touches the network.

## Run the integration tests on demand

From the solution root (`lighthouse-back/`):

```bash
# Linux / macOS
RUN_REGISTRY_INTEGRATION=1 dotnet test --filter Category=RegistryIntegration
```

```powershell
# Windows (PowerShell)
$env:RUN_REGISTRY_INTEGRATION = "1"; dotnet test --filter Category=RegistryIntegration
```

Add `--logger "console;verbosity=detailed"` to print the resolved digests and
creation dates for each image.

They check that Docker Hub, GHCR and a generic OCI registry (MCR) each resolve a
`sha256:` digest for a known public image, that the HEAD and GET paths agree (the
invariant the image-update check relies on), and that a creation date is returned.
If an image/tag used here is ever retired, update the references in
`RegistryClientsIntegrationTests.cs`.

## Run only the (fast, offline) characterization tests

```bash
dotnet test --filter "FullyQualifiedName~RegistryClientTests"
```
