## Why

The Blazor app is scaffolded but unusable: the default Counter/Weather pages are still there, there is no login screen, no auth guard on any page, and the Query page (which does work via server-side DI) isn't even linked in the nav. Doctors cannot use the system and admins cannot upload or re-index books without hitting raw API endpoints. The UI needs to become a real product.

## What Changes

Two separate areas — public and admin — both behind a cookie-based login:

**Auth (shared)**
- Add ASP.NET Core cookie authentication alongside the existing JWT (which stays for the REST API)
- Login page at `/login` — validates against the same config-based user list, signs in with a cookie
- Logout action clears the cookie and redirects to `/login`
- `CascadingAuthenticationState` + `AuthorizeRouteView` wraps the app so unauthenticated requests redirect to `/login`

**Public area** (role: Doctor or Admin)
- `/query` — polished query page: language selector, query type, book checkboxes, streaming-friendly answer display with source citations, web-search toggle
- Nav: MedAssist brand, Query link, Logout

**Admin area** (role: Admin)
- `/admin/books` — list all books (title, author, language, status, chunk count, indexed date), trigger re-index button per book
- `/admin/books/upload` — upload new PDF form (book ID, title, author, language, edition, file picker), submits to existing `POST /api/admin/books/upload`
- Nav: admin section with Books link visible only to Admin role

## Capabilities

### New Capabilities

- `blazor-auth`: Cookie-based login/logout for Blazor pages
- `blazor-query-ui`: Public query interface for doctors
- `blazor-admin-ui`: Admin book management interface

### Modified Capabilities

- None — REST API endpoints and their auth are unchanged

## Impact

- `MedAssist.Web/Program.cs` — add `AddCookie` to auth pipeline
- `MedAssist.Web/Components/App.razor` — add `CascadingAuthenticationState`
- `MedAssist.Web/Components/Routes.razor` — swap to `AuthorizeRouteView`
- `MedAssist.Web/Components/Layout/MainLayout.razor` — add logout button, role-conditional admin links
- `MedAssist.Web/Components/Layout/NavMenu.razor` — clean up, add Query and Admin links
- `MedAssist.Web/Components/Pages/Login.razor` — new
- `MedAssist.Web/Components/Pages/Query.razor` — polish existing
- `MedAssist.Web/Components/Pages/Admin/Books.razor` — new
- `MedAssist.Web/Components/Pages/Admin/UploadBook.razor` — new
- `MedAssist.Web/Services/AdminBookService.cs` — new (wraps HTTP calls to admin API endpoints)
- Delete: `Counter.razor`, `Weather.razor`, `Home.razor` (replace with redirect to `/query`)
