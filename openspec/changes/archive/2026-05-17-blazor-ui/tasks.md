## 1. Auth Infrastructure

- [x] 1.1 Add `.AddCookie("Cookies", ...)` to `AddAuth` in `ServiceCollectionExtensions.cs`, chained after `AddAuthenticationJwtBearer`
- [x] 1.2 Wrap `<Routes />` in `App.razor` with `<CascadingAuthenticationState>`
- [x] 1.3 Replace `<RouteView>` with `<AuthorizeRouteView>` in `Routes.razor`; add `<NotAuthorized>` template that navigates to `/login`

## 2. Login / Logout

- [x] 2.1 Create `MedAssist.Web/Components/Pages/Login.razor` — SSR-only page at `/login` with username/password form; on POST validate against `Auth:Users` config, call `HttpContext.SignInAsync("Cookies", ...)`, redirect to `/query` on success or show error on failure
- [x] 2.2 Add a `LogoutEndpoint` (FastEndpoints GET endpoint at `/logout`) or a Blazor logout page that calls `HttpContext.SignOutAsync("Cookies")` and redirects to `/login` with `forceLoad:true`

## 3. Navigation Cleanup

- [x] 3.1 Rewrite `NavMenu.razor`: remove Counter/Weather links; add "Query" link to `/query`; add "Books" link under an admin section visible only when user has Admin role; add "Logout" link
- [x] 3.2 Update `MainLayout.razor`: remove default scaffold markup; expose `@Body` cleanly; remove the existing logout button or wire it to the logout action

## 4. Delete Scaffold Pages

- [x] 4.1 Delete `Counter.razor` and `Weather.razor`
- [x] 4.2 Replace `Home.razor` with a redirect component that navigates to `/query` on load (or just delete and make `/query` the default route)

## 5. Query Page

- [x] 5.1 Add `@attribute [Authorize(Roles="Doctor,Admin")]` to `Query.razor`
- [x] 5.2 Add language selector dropdown (English / Bulgarian) bound to a `language` field
- [x] 5.3 Add query type selector (Book Search / Book Search + Web) bound to a `useWebSearch` bool
- [x] 5.4 Add book filter checkboxes: call `BookCatalogService` to get the book list; bind checked IDs to a `selectedBookIds` set
- [x] 5.5 Wire the submit button to call `QueryService.QueryAsync(query, language, useWebSearch, selectedBookIds.ToArray())` and store the result
- [x] 5.6 Display the answer text and a collapsible "Sources" section with citation rows (title, author, chapter, section, pages)

## 6. Admin Book Service

- [x] 6.1 Create `MedAssist.Web/Services/AdminBookService.cs` — scoped service; on first use call `POST /api/auth/login` with admin credentials from `IConfiguration["Auth:Users"]` to obtain a JWT; cache it for the service lifetime
- [x] 6.2 Implement `GetBooksAsync()` — GET `/api/admin/books`, return list of book DTOs
- [x] 6.3 Implement `TriggerReindexAsync(bookId)` — POST `/api/admin/books/{bookId}/index`
- [x] 6.4 Implement `UploadBookAsync(formData)` — POST multipart to `/api/admin/books/upload`
- [x] 6.5 Register `AdminBookService` as scoped in `ServiceCollectionExtensions.AddQueryServices`

## 7. Admin Pages

- [x] 7.1 Create `MedAssist.Web/Components/Pages/Admin/Books.razor` at route `/admin/books` with `@attribute [Authorize(Roles="Admin")]`; load books from `AdminBookService.GetBooksAsync()` and render a table (title, author, language, status, chunk count, indexed date) with a "Re-index" button per row
- [x] 7.2 Wire the "Re-index" button to call `AdminBookService.TriggerReindexAsync(bookId)`; show spinner while running, show success/error inline
- [x] 7.3 Create `MedAssist.Web/Components/Pages/Admin/UploadBook.razor` at route `/admin/books/upload` with `@attribute [Authorize(Roles="Admin")]`; form fields: book ID, title, author, language dropdown (EN/BG), edition, PDF file picker; submit calls `AdminBookService.UploadBookAsync`; show success message or API error
- [x] 7.4 Create `MedAssist.Web/Components/Layout/AdminLayout.razor` — inherits main shell, adds admin nav links (Books, Upload Book)
- [x] 7.5 Set `@layout AdminLayout` on both admin pages
