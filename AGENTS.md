# AGENTS.md

## Purpose

This file guides Codex when writing, editing, refactoring, or reviewing code for this .NET project. Codex must prioritize clear, maintainable, idiomatic C# code with appropriate comments and complete XML documentation.

---

## Preferred Language and Framework

- Use modern C# with clear, readable syntax.
- The project may use ASP.NET Core, ASP.NET Core Web API, Entity Framework Core, LINQ, Dependency Injection, JWT, microservices, or layered architecture.
- When no specific version is requested, prefer .NET 8 or the latest suitable LTS version used by the project.
- Do not add external packages unless they are truly necessary.
- If a package must be added, explain why it is needed.

---

## Mandatory Comments and Documentation Rules

### 1. XML Summary for Every Type

Every `class`, `interface`, `record`, `struct`, and `enum` must have an XML documentation comment.

Example:

```csharp
/// <summary>
/// Represents a user stored in the system.
/// This class is used to map user data to the Users table.
/// </summary>
public class User
{
}
```

### 2. XML Summary for Every Method and Constructor

Every method and constructor must have XML documentation. This applies to `public`, `protected`, `internal`, and private methods when they contain meaningful logic.

Each method documentation should include:

- `<summary>`: describes the main responsibility of the method.
- `<param>`: describes each parameter.
- `<returns>`: describes the returned value when the method returns data.
- `<exception>`: describes possible exceptions when relevant.

Example:

```csharp
/// <summary>
/// Gets a user by the specified identifier.
/// </summary>
/// <param name="id">The unique identifier of the user.</param>
/// <returns>
/// Returns the matching user when found; otherwise, returns null.
/// </returns>
public async Task<User?> GetUserByIdAsync(int id)
{
}
```

### 3. XML Summary for Important Properties

Properties in entities, DTOs, configuration classes, request models, response models, and service contracts must have concise XML summaries.

Example:

```csharp
/// <summary>
/// Email address used by the user to sign in and receive notifications.
/// </summary>
public string Email { get; set; } = string.Empty;
```

### 4. Comments for Important Code Lines and Logic Blocks

Important lines of code and important logic blocks must have comments explaining why the code exists or what business rule it protects.

Add comments for:

- Business rules.
- Complex conditions.
- Validation rules that are not obvious.
- Error handling decisions.
- Security-sensitive checks.
- Data transformation rules.
- Non-trivial LINQ queries.
- External integration behavior.
- Workarounds or project-specific constraints.

Example:

```csharp
// Orders that have already been paid cannot be modified because payment data must remain auditable.
if (order.Status == OrderStatus.Paid)
{
    throw new InvalidOperationException("Paid orders cannot be updated.");
}
```

Do not comment obvious lines that do not add useful context.

Avoid:

```csharp
// Increase i by 1.
i++;
```

---

## Naming Conventions

- Use PascalCase for classes, interfaces, enums, methods, and properties.
- Use camelCase for local variables and parameters.
- Interface names must start with `I`, for example `IUserService`.
- Async method names must end with `Async`, for example `GetUsersAsync`.
- DTO names should end with `Dto`, for example `CreateUserDto`.
- Request model names should end with `Request`, for example `LoginRequest`.
- Response model names should end with `Response`, for example `LoginResponse`.

---

## C# Coding Rules

- Prefer readable code over overly compact code.
- Do not put long business logic inside controllers.
- Controllers should receive requests, call services, and return responses.
- Business logic must be placed in services.
- Database access logic should be placed in repositories or DbContext, depending on the project architecture.
- Do not hard-code connection strings, secret keys, JWT keys, passwords, or sensitive values.
- Use `appsettings.json`, environment variables, or secret manager for sensitive configuration.
- Always validate input before processing it.
- Always handle null values intentionally.
- Prefer `async/await` for I/O operations such as database, file, and HTTP calls.
- Do not use `.Result` or `.Wait()` with `Task` unless there is a clearly documented reason.

---

## ASP.NET Core Web API Rules

### Controllers

- Controllers must have clear routes.
- Every action method must have XML documentation.
- Actions should return `ActionResult<T>` or `IActionResult`.
- HTTP status codes must be appropriate:
  - `200 OK` for successful read or processing operations.
  - `201 Created` when a resource is created successfully.
  - `204 NoContent` when an update or delete succeeds without a response body.
  - `400 BadRequest` when input is invalid.
  - `401 Unauthorized` when authentication is missing or invalid.
  - `403 Forbidden` when the user does not have permission.
  - `404 NotFound` when the resource does not exist.
  - `500 InternalServerError` for unexpected system errors.

Example:

```csharp
/// <summary>
/// Gets all users currently available in the system.
/// </summary>
/// <returns>
/// Returns the user list as UserResponse objects.
/// </returns>
[HttpGet]
public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsersAsync()
{
    var users = await _userService.GetUsersAsync();
    return Ok(users);
}
```

### Services

- Services contain the main business logic.
- Create a service interface when the service is injected through Dependency Injection.
- Every service method must have XML documentation.
- Important business rules inside services must be explained with comments.

Example:

```csharp
/// <summary>
/// Provides business operations related to users.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets all users currently available in the system.
    /// </summary>
    /// <returns>Returns the list of users.</returns>
    Task<IEnumerable<UserResponse>> GetUsersAsync();
}
```

---

## Entity Framework Core Rules

- Entities must have XML summaries for the class and important properties.
- DbContext classes must have XML summaries.
- DbSet properties must have XML summaries.
- Configure Fluent API in `OnModelCreating` or split it into `IEntityTypeConfiguration<T>` classes for larger models.
- Do not use lazy loading unless it is clearly needed.
- Use `AsNoTracking()` for read-only queries.
- Include related data only when it is actually needed.
- Add comments for non-obvious query filters, includes, projections, or performance-related decisions.

Example:

```csharp
/// <summary>
/// Represents the main database context of the application.
/// This context manages DbSet properties and entity mapping configuration.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Users table in the system.
    /// </summary>
    public DbSet<User> Users => Set<User>();
}
```

---

## Error Handling Rules

- Do not swallow exceptions with empty `catch` blocks.
- When catching exceptions, log them or convert them into meaningful application errors.
- Do not return sensitive exception messages directly to clients in production.
- Prefer global exception-handling middleware.
- Use custom exceptions for business errors when appropriate.

Example:

```csharp
/// <summary>
/// Exception used when a requested resource cannot be found.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the NotFoundException class with a specific message.
    /// </summary>
    /// <param name="message">Message describing the missing resource.</param>
    public NotFoundException(string message) : base(message)
    {
    }
}
```

---

## Validation Rules

- Request DTOs must use data annotations or validators when validation is required.
- Never trust data from clients.
- Validate required fields, length, email format, phone format, dates, ranges, and business constraints.
- Add comments when a validation rule represents a business decision that is not obvious from the attribute or condition.

Example:

```csharp
/// <summary>
/// Request data used to create a new user.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Full name of the user.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Email used for sign-in and contact.
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}
```

---

## Dependency Injection Rules

- Do not instantiate services with `new` inside controllers.
- Register services in `Program.cs` or in a dedicated extension method.
- Prefer constructor injection.

Example:

```csharp
/// <summary>
/// Controller that handles user-related APIs.
/// </summary>
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the UsersController class.
    /// </summary>
    /// <param name="userService">Service that provides user-related business operations.</param>
    public UsersController(IUserService userService)
    {
        _userService = userService;
    }
}
```

---

## Security Rules

- Do not store passwords as plain text.
- Passwords must be hashed with a secure algorithm.
- Do not log tokens, passwords, secret keys, or sensitive personal data.
- JWT configuration must define issuer, audience, expiry time, and signing key.
- APIs that require authorization must use `[Authorize]`.
- Use `[AllowAnonymous]` only when the endpoint is intentionally public.
- Check user permissions before modifying or deleting data.
- Add comments for security-sensitive checks so the protected rule is clear.

---

## Testing Rules

- Add unit tests when new business logic is introduced.
- Test names should describe the condition and expected result.
- Suggested test naming pattern: `MethodName_StateUnderTest_ExpectedBehavior`.
- Test methods should also have XML summaries when the test project follows documentation generation.

Example:

```csharp
/// <summary>
/// Verifies that GetUserByIdAsync returns null when the user does not exist.
/// </summary>
[Fact]
public async Task GetUserByIdAsync_UserNotFound_ReturnsNull()
{
}
```

---

## Refactoring Rules

- Do not change business behavior unless the user requests it.
- Preserve public APIs unless there is a clear reason to change them.
- After refactoring, make sure the code still builds.
- If a class, method, or property is renamed, update every usage.
- When extracting a method, the new method must also have XML documentation unless it is a very small private helper with no meaningful logic.
- Add comments to extracted logic when the reason for the split or the business rule is not obvious.

---

## New File Rules

When Codex creates a new file, ensure that:

1. The namespace matches the project structure.
2. Required `using` statements are present and unnecessary ones are avoided.
3. Every class, interface, record, struct, and enum has XML documentation.
4. Every method and constructor has XML documentation.
5. Important properties have XML documentation.
6. Important lines and logic blocks have explanatory comments.
7. Code formatting is clean and readable.
8. Dependency Injection registration is not missed when a service needs to be injected.
9. No unfinished sample code is created when the user asks for working code.

---

## Response Rules After Code Changes

When Codex changes code, respond briefly with:

- Which files were changed.
- What logic was added or updated.
- Whether any class or method structure changed.
- Whether migration, build, or tests should be run.

Example response:

```text
Updated UserService.cs:
- Added XML documentation for the class and all methods.
- Added a comment explaining the duplicate email business rule.
- Changed read-only queries to use AsNoTracking().

Recommended checks:
dotnet build
dotnet test
```

---

## Postman API Synchronization

- Whenever public REST endpoints, Gateway routes, request or response DTOs, authentication behavior, or end-to-end workflows change, inspect and update the `Commerce Microservices API` Postman collection using the Postman MCP server.
- Treat the application source code as the source of truth for Postman requests, scripts, tests, and environment variables.
- Route all normal public requests through Commerce.Gateway and YARP. Do not expose internal gRPC or RabbitMQ operations as public HTTP requests.
- Never upload production secrets to Postman.

---

## Final Quality Checklist

Before finishing, Codex must verify:

- The code can build.
- No required `using` statements are missing.
- Every class, interface, record, struct, and enum has XML documentation.
- Every method and constructor has XML documentation.
- Important properties have XML documentation.
- Important code lines and logic blocks have explanatory comments.
- No secrets are hard-coded.
- Null values and errors are handled intentionally.
- `async/await` is used correctly.
- Names are clear and consistent.

---

## Code Style Priorities

Generated or edited code must prioritize:

1. Readability.
2. Maintainability.
3. Correct C#/.NET conventions.
4. Complete documentation.
5. Useful comments on important code.
6. Avoiding over-engineering.
7. Suitability for students or developers building a real-world project.

---

## React Frontend Rules

### Ownership and architecture

- Frontend source belongs to the independent `vixtruong/commerce-web` repository, normally checked out at `../Commerce.Web`. `src/Web` is not a place to vendor React source.
- Backend owns public contracts, permissions and full-stack Compose integration. Use `FRONTEND_SOURCE_PATH` for an alternate frontend checkout; integration CI checks out both repositories explicitly.
- Keep TypeScript strict, use explicit API DTOs and avoid `any` or unchecked casts. Do not expose backend domain/EF entities as frontend contracts.
- Organize by feature with shared controls, layouts, API client, authentication and formatting modules. Use existing dependencies before adding packages.
- Browser requests go through YARP only. Never call service ports, internal gRPC, RabbitMQ or databases from React.

### Design system and UI structure

- **SkillNest is the primary visual reference**: calm mint canvas, white sidebar/header, mint product stages, dark emerald accents, restrained icon tiles and monospace metadata. Logbook supplements content hierarchy and spacing. DuelSheet is excluded.
- Design structure around the user task. Storefront uses a compact search header, actual merchandise early, a full-width catalog toolbar/grid and a focused purchase panel. Backoffice uses a KPI strip, action queues and dense operational tables/forms. Do not preserve an old layout solely to restyle it.
- Keep corners restrained: controls 4px, surfaces 6px, dialogs at most 8px. Only avatars and status dots are circular. Prefer whitespace and separators to nested cards and decorative pills.
- Use semantic tokens in the frontend's `src/styles/tokens.css`. `index.css` imports `base.css`, `storefront.css`, `commerce.css`, `admin.css` and `responsive.css`; keep breakpoints together and avoid competing overrides.
- Use proportional display typography for strong headings, monospace for SKU/eyebrow/workspace metadata and body typography for long text/forms. System fonts must work offline and with the current CSP.
- Reuse `Brand`, `PageHeader`, feedback, status, table, pagination and checkout navigation components. Use existing Lucide icons; every interactive control must have a supported action.
- Do not invent discounts, reviews, records, metrics or stock/health indicators. Data-dependent copy must come from authoritative responses.
- Render Catalog-owned same-origin photos and ordered galleries; identify legacy seed illustrations as sample artwork and show explicit fallback when a photo fails or is unavailable. Product editors upload up to eight JPEG/PNG/WebP photos through the authorized Catalog API.
- Inspect backend use cases before adding refunds, password changes, editable profiles, carrier tracking or category assignment.

### State, forms and navigation

- Use TanStack Query for server state, profile, cart and summaries; keep Zustand limited to UI preferences/navigation.
- Keep search/filter/sort/pagination in URL parameters. Reset related filters together and reset the page when filters change; preserve debounce and placeholder-response pagination guards.
- Use React Hook Form and Zod. Preserve dirty-form warnings, autocomplete, field-level accessible errors and server validation mapping.
- Centralize formatting and status labels. Calculate totals using integer cents and group by currency; send original backend status values even when labels are human-readable.
- Never automatically retry mutations. Preserve duplicate-submit guards and existing confirmation dialogs.

### Security and workflow invariants

- Centralize permissions in `src/auth/permissions.ts`; use permission guards and `Can`. Roles are Identity-managed bundles. Backend policies and resource ownership remain authoritative.
- Preserve refresh single-flight, tab-scoped credentials, session-generation checks, private-query cancellation/cleanup and safe same-origin return paths.
- Derive command palette and sidebar results from effective permissions; never expose a forbidden area through search.
- Password visibility must not submit forms, alter entered values or log credentials. Never commit tokens, secrets, local environment content or sensitive test artifacts.
- Preserve owner-scoped durable checkout idempotency keys and immutable payloads saved before submission; network failures, remounts and session renewal must reuse the attempt.
- Preserve serialized optimistic cart writes and rollback. Cart clearing after accepted checkout remains explicit.
- `CheckoutSteps` shows page location only; asynchronous Saga progress and compensation must follow persisted backend states. Do not invent transition timestamps or label shipment creation as delivery.
- Stop polling at terminal state, error, unmount or timeout and retain manual resume. Report released stock only after compensation completes.

### Accessibility and responsive quality

- Provide keyboard navigation, visible focus, skip links, route focus, labels, heading hierarchy, appropriate button types and active route/step state.
- Use Radix dialogs/drawers with title, description, focus handling and Escape support; use native disclosure for catalog filters.
- Target at least 44px for primary controls. Preserve readable contrast (4.5:1 for text), status text in addition to color, and reduced motion.
- Provide meaningful loading/error/empty states with actionable retry when supported. Avoid unexplained blank panels.
- Preserve table captions/headers and visible mobile field labels. Table containers may scroll locally; the document must not overflow horizontally.
- Review important layouts at 375, 768 and 1440px with real and long content, including auth, product, cart/checkout, dashboard, tables and forms.

### Verification and documentation

- Test behavior with Vitest/Testing Library/MSW and real Gateway workflows with Playwright. Preserve refresh, idempotency, permission, ownership and compensation coverage.
- Run `pnpm lint`, `pnpm typecheck`, `pnpm test` and `pnpm build`; run `pnpm build-storybook` when shared UI/stories change. Report actual outcomes and unverified service limitations.
- E2E credentials come from ignored frontend `.env.e2e` or explicit `E2E_ENV_FILE`; never commit them.
- Keep Storybook, README, route/API documentation, Docker and CI aligned with implemented capabilities. Public REST/DTO/Gateway/auth or end-to-end business workflow changes require the Postman synchronization procedure above.
- Research, implementation plan and verification record live in `../Commerce.Web/docs/frontend-redesign-plan.md`. The frontend's own AGENTS contains its detailed working rules.
