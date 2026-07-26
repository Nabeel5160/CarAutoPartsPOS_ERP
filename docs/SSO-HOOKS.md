# SSO design hooks (M4)

SSO is **not implemented** in v1. Extension points:

1. **JWT issuance** — `JwtTokenService.CreateToken` can accept externally validated identity claims (`sub`, email, roles) after an OIDC callback.
2. **Claims** — already uses `company_id`, `branch_id`, and `permission` claim types suitable for mapping from IdP group → CAP roles.
3. **Future package** — add `Microsoft.AspNetCore.Authentication.OpenIdConnect` beside JWT bearer; keep API resource-server mode for WPF/Blazor.
4. **User provisioning** — map IdP subject to `AppUser` via optional `ExternalSubject` column (add when enabling SSO).
