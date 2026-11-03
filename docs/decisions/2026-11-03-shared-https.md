# HTTPS for a shared server

Asked 2026-11-03, when the owner chose it as the next step (backlog, Maybe: the web UI's next steps). Until then
`mediaqc serve --listen` shared over plain HTTP (decisions/2026-11-01-shared-machine.md), so the token link and the
files' names crossed the network unencrypted. A localhost-only `serve` stays plain HTTP: nothing leaves the machine.

## Where the certificate comes from

- **Yours when given, else a self-signed one** (recommended): `--cert FILE` (a .pfx, or a PEM certificate with
  `--key FILE`) uses one the company has; without, mediaqc makes a self-signed certificate once and keeps it in the
  user's profile, so it stays the same from run to run, and prints its SHA-256 fingerprint so the team can check the
  browser's one-time warning is about this server.
- Only a given certificate: no warnings, but someone must get a certificate for the machine first.
- Only self-signed: the simplest, but every browser warns once and a company's certificate can't be used.

**Answer: yours when given, else self-signed.**

## The default

- **HTTPS when shared, `--http` opts out** (recommended): encrypted unless plain HTTP is asked for; changes what
  `--listen` did.
- HTTP by default, `--https` opts in: as before.
- Always HTTPS when shared: no plain HTTP at all.

**Answer: HTTPS when shared, `--http` opts out.**

## What was built

- `Serve/Certificates.cs`: `Load(file, key)` (a .pfx, its password in `MEDIAQC_CERT_PASSWORD` if it has one; a PEM
  certificate with its key, or one PEM file holding both; a PEM key goes through PKCS#12, as Windows' TLS can't use an
  ephemeral key), `SelfSigned(file, machine, now)` (ECDSA P-256, two years, server authentication; named for the
  machine, localhost and the loopback addresses; kept in LocalApplicationData's `mediaqc/serve-certificate.pfx`,
  readable by the user only on Linux and macOS; made again when it is for another name, within 30 days of expiring, or
  unreadable) and `Fingerprint` (SHA-256, colon-separated hex, as browsers show it).
- `mediaqc serve --listen ... [--cert FILE [--key FILE] | --http]`: Kestrel serves HTTPS with the certificate; the
  printed link is https://, with the self-signed certificate's file and fingerprint (or, with --http, a line that it is
  unencrypted); the token cookie is Secure over HTTPS. `--cert`, `--key` and `--http` need `--listen`; `--key` needs
  `--cert`; `--http` takes neither. A certificate that can't be read stops serve before it listens (exit 2).
- HSTS isn't sent: with a self-signed certificate a browser would then refuse the page outright once the certificate
  changes, rather than warn.
