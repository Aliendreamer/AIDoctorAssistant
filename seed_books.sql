-- Re-seed the books table after the DB was wiped.
-- Topology note: Postgres now lives in the PersonalCommandCenter stack
--   (container personalcommandcenter-postgres-1, user "pcc", database "medassist").
--
-- Run with:
--   docker exec -i personalcommandcenter-postgres-1 \
--     psql -U pcc -d medassist < seed_books.sql
--
-- status: native PG enum book_status with lowercase labels
--   ('pending','in_progress','indexed','failed').
-- All rows seeded as 'pending'; run the indexer to populate chunks / flip to 'indexed'.
-- book_id == the markdown filename stem in books/mdFiles/<book_id>.md (and the cache
-- key the indexer reads). file_path points at the (currently absent) source PDF.

INSERT INTO books (book_id, title, author, language, edition, file_path, total_chunks, status)
VALUES
  ('basics-pathofisiology',                 'Основи на патофизиологията',                  '',                    'bg', '',          '/books/uniquebooks/basics-pathofisiology.pdf',                 0, 'pending'),
  ('basics-pediatrics',                      'Основи на педиатрията',                       '',                    'bg', '',          '/books/uniquebooks/basics-pediatrics.pdf',                      0, 'pending'),
  ('emergency-pediatrics',                   'Спешна педиатрия',                            '',                    'bg', '',          '/books/uniquebooks/emergency-pediatrics.pdf',                   0, 'pending'),
  ('emergency-pediatrics-boikinov-shmilev',  'Спешна педиатрия (Бойкинов, Шмилев)',         'Бойкинов, Шмилев',    'bg', '',          '/books/uniquebooks/emergency-pediatrics-boikinov-shmilev.pdf',  0, 'pending'),
  ('emergency-pediatrics-gastroenterlogy',   'Спешна педиатрия — гастроентерология',        '',                    'bg', '',          '/books/uniquebooks/emergency-pediatrics-gastroenterlogy.pdf',   0, 'pending'),
  ('pediatrics-5th-edition',                 'Педиатрия — илюстрован учебник',              'Tom Lissauer, Will Carroll', 'bg', '5-то издание', '/books/uniquebooks/pediatrics-5th-edition.pdf',           0, 'pending'),
  ('pediatrics-litvinenko',                  'Педиатрия (Литвиненко)',                      'Литвиненко',          'bg', '',          '/books/uniquebooks/pediatrics-litvinenko.pdf',                  0, 'pending'),
  ('pediatrics-varna',                       'Педиатрия (Варна)',                           '',                    'bg', '',          '/books/uniquebooks/pediatrics-varna.pdf',                       0, 'pending'),
  ('poisonsandinjuries',                     'Отравяния и злополуки в детската възраст',    '',                    'bg', '',          '/books/uniquebooks/poisonsandinjuries.pdf',                     0, 'pending'),
  ('practical-pediatrics',                   'Практическа педиатрия',                       '',                    'bg', '',          '/books/uniquebooks/practical-pediatrics.pdf',                   0, 'pending')
ON CONFLICT (book_id) DO NOTHING;

-- Verify:
--   SELECT id, book_id, title, language, status, total_chunks FROM books ORDER BY id;
