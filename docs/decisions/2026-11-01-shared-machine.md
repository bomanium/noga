# Shared-machine mode for mediaqc serve

Question 1: what next after the EBU QC items.

Choices:

1. A shared-machine mode (recommended): `mediaqc serve --listen ADDRESS:PORT` for a team's machine on the network,
   with a token in the URL; localhost stays the default.
2. A native app shell around the page.
3. Platform spec templates (profiles beyond EBU R128).
4. Stop here for now.

Answer: 1.

Question 2: which folders people on the network can browse and check.

Choices:

1. Only the folders named with `--root` (and the `--watch` folder) when listening beyond localhost; the rest of the
   machine stays invisible; localhost keeps the whole-machine picker (recommended).
2. The whole machine, as on localhost.

Answer: 1.

Question 3: how people on the network get in.

Choices:

1. A token link: mediaqc prints a link with a random token; opening it sets a cookie, requests without it get 401;
   `--token` sets a fixed one for bookmarks; plain HTTP, for trusted networks (recommended).
2. As 1, over HTTPS with a self-signed or given certificate.
3. No sign-in.

Answer: 1.

With the code: `mediaqc serve --listen 0.0.0.0:8722 --root FOLDER [--root ...] [--token TOKEN]`. A server listening
beyond 127.0.0.1 refuses to start without a root (or a watched folder) and makes a random 128-bit token unless given
one (16 characters or more). The link's token goes into an HttpOnly, SameSite=Strict cookie and the page is
redirected to the address without it; every request needs the cookie (compared in constant time), so another site's
page can't use the server (DNS rebinding included, which is why the localhost Host check is left out when shared).
The page's header and same-origin checks still hold for changes. Folders and checks are limited to the roots
(`Roots`): a path inside one, with no symbolic link or junction on the way (links inside roots aren't listed), so
`..` and links can't lead out. `--root` limits a localhost server too. Plain HTTP: the token and the files' names
cross the network unencrypted, so it is for trusted networks; HTTPS stays a later option.
