# PR Brain — Team Standards

Copy this file to `.github/review-brain.md` in any repo you want PR Brain to review.
Customise each section for your team. PR Brain will enforce these on every review.

---

## Architecture Rules
- Follow Clean Architecture: Api → Application → Domain → Infrastructure
- No business logic in controllers — delegate to services/handlers
- No direct DbContext usage outside of Infrastructure layer
- Use the Result pattern for error handling — no raw exceptions across layer boundaries

## API Design
- All endpoints must return consistent `ApiResponse<T>` wrapper
- Use `[ProducesResponseType]` attributes for all possible responses
- Validate inputs with FluentValidation — never validate in controllers
- Idempotency keys required on all POST endpoints that mutate state

## Database & Transactions
- Only wrap in transactions when multiple writes must be atomic
- Never use `SaveChanges()` inside a loop
- All queries must have cancellation token support

## Security
- All endpoints require `[Authorize]` unless explicitly marked `[AllowAnonymous]`
- Never log request bodies containing PII
- Validate and sanitise all external inputs before use

## Error Handling
- Never swallow exceptions silently — log and re-throw or handle explicitly
- Use structured logging with Serilog — no `Console.WriteLine`
- All external API calls must have timeout and retry policies (Polly)

## Testing Requirements
- Unit tests required for all service/handler methods
- Integration tests required for all API endpoints
- Test names must follow: `MethodName_Scenario_ExpectedResult`
- Mocks via NSubstitute — no Moq

## Code Style
- Nullable reference types enabled — handle all nulls explicitly
- File-scoped namespaces
- No magic strings — use constants or enums
- XML docs on all public interfaces
