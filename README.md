# OdjfsScraper.SyncDatabaseBackups

A tool to upload OdjfsScraper database backups to a remote server via SFTP.

## Usage

```
Usage: SyncDatabaseBackups [OPTIONS]

  Uploads all OdjfsScraper database backups to the specified remote host,
  via SFTP. Only database backups with filenames matching the following
  pattern will be included:

  OdjfsScraper_####-##-##.bak (# must be a digit, 0-9)

  A symbolic link 'OdjfsScraper_Current.bak' will be created in the
  remote directory pointing to the last database backup in the remote
  directory, by lex order.

Options:
  -o=VALUE                   the remote host
  -u=VALUE                   the SFTP username
  -p=VALUE                   the SFTP password
  -l=VALUE                   the local source directory of database backups
  -r=VALUE                   the remote destination directory for the
                               database backups
  -i                         use case-insensitive file name comparison,
                               default: false
  -h, --help                 display this help message
```