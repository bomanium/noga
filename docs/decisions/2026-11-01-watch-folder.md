# Watch folder

Question 1: what next after the web UI.

Choices:

1. A watch folder, `mediaqc serve --watch FOLDER` (recommended): post houses' hand-off folders.
2. Open films in CI.
3. A native app shell around the page.
4. Stop here for now.

Answer: 1.

Question 2: which files a watched folder's checks take.

Choices:

1. New and unchecked (recommended): at the start, the media files in the folder and its subfolders that have no
   FILE.qc.json yet; then each new one once its size has stayed the same for 5 s (its copy has finished); reports
   beside each file.
2. New files only, after mediaqc starts.
3. As 1, the top folder only.

Answer: 1.

With the code: Watcher scans the folder every 2 s rather than following file-system events, which network shares
(where hand-off folders often are) don't deliver reliably; a file counts as copied when its size and time are the
same for 5 s. Hidden files and folders, reports and non-media extensions are left alone; each file is checked once
per run (a file replaced later keeps its first report until mediaqc runs again and the report is removed). The page
says which folder is watched and its picker opens there.
