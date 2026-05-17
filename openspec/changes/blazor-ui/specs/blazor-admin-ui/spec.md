## ADDED Requirements

### Requirement: Admin area restricted to Admin role
All pages under `/admin/*` SHALL require authentication AND the Admin role. A Doctor-role user who navigates to any admin URL SHALL be shown an "Access Denied" message, not redirected to login.

#### Scenario: Doctor denied admin access
- **WHEN** a Doctor-role user navigates to `/admin/books`
- **THEN** the page shows "Access Denied" and does not render the books list

#### Scenario: Unauthenticated admin access redirects to login
- **WHEN** an unauthenticated user navigates to `/admin/books`
- **THEN** the system redirects to `/login`

### Requirement: Book list page
The `/admin/books` page SHALL display a table of all books known to the system. Each row SHALL show: title, author, language, status, chunk count, and indexed date. The page SHALL load book data by calling `GET /api/admin/books`.

#### Scenario: Books listed on load
- **WHEN** an Admin-role user navigates to `/admin/books`
- **THEN** the page displays a table row for each book in the system

#### Scenario: Empty state
- **WHEN** no books exist in the system
- **THEN** the page shows "No books uploaded yet" and a link to upload

### Requirement: Re-index trigger
Each row in the books list SHALL include a "Re-index" button. Clicking it SHALL call `POST /api/admin/books/{bookId}/index` and show inline status feedback (spinning while running, success or error message on completion).

#### Scenario: Re-index succeeds
- **WHEN** the user clicks "Re-index" on a book and the indexing completes
- **THEN** the row updates to show "Indexed" status and the new indexed date

#### Scenario: Re-index in progress
- **WHEN** the user clicks "Re-index" and indexing is still running
- **THEN** the button is disabled and shows a spinner until the operation completes or returns an error

### Requirement: Upload book page
The `/admin/books/upload` page SHALL provide a form to upload a new PDF book. The form SHALL collect: book ID (text, unique), title, author, language (EN/BG dropdown), edition, and a PDF file picker. Submitting SHALL POST a multipart form to `POST /api/admin/books/upload`.

#### Scenario: Successful upload
- **WHEN** the user fills all required fields, selects a valid PDF, and submits
- **THEN** the form POSTs to the upload endpoint, and on success the page shows "Book uploaded successfully" and a link back to the books list

#### Scenario: Missing required fields
- **WHEN** the user submits the form with a required field empty
- **THEN** the form shows validation errors and does not POST

#### Scenario: Upload error from API
- **WHEN** the upload endpoint returns an error (e.g., duplicate book ID, invalid file)
- **THEN** the page shows the error message from the API response

### Requirement: Admin navigation
The admin area SHALL use a layout that includes the main nav (Query, Logout) plus an admin sidebar or nav section with a "Books" link to `/admin/books` and an "Upload Book" link to `/admin/books/upload`.

#### Scenario: Admin nav links present
- **WHEN** an Admin-role user is in the admin area
- **THEN** the nav includes links to "Books" and "Upload Book" in addition to "Query" and "Logout"
