# Importer moved

`Gb64Import`, `Gb64Import.Tests`, and `HvscFetch` now live in `F:\GitHub\vice-sharp-romm\tools\`.

RomM `scripts\Prepare-RomMLibrary.ps1` and `scripts\Download-Hvsc.ps1` forward to that repo and keep this tree as the library data root (`gb64/`, `runtime/library`).

Set `VICESHARP_ROMM_ROOT` if vice-sharp-romm is not a sibling of this repo.
