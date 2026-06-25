# HeimdallHash - Guía de instalación

## Instalación estándar

1. Descomprimir `HeimdallHash_Release_vX.Y.Z.zip`.
2. Abrir PowerShell como Administrador.
3. Entrar en la carpeta `Scripts`.
4. Ejecutar:

```powershell
.\Install-HeimdallHash.ps1
```

Por defecto instala en:

```text
C:\Program Files\HeimdallHash
```

## Abrir configurador

```text
C:\Program Files\HeimdallHash\Configurador\HeimdallhashConfigurator.exe
```

## Iniciar servicio

```powershell
.\Start-HeimdallHash.ps1
```

## Detener servicio

```powershell
.\Stop-HeimdallHash.ps1
```

## Desinstalar servicio

```powershell
.\Uninstall-HeimdallHash.ps1
```

Para eliminar también archivos instalados:

```powershell
.\Uninstall-HeimdallHash.ps1 -RemoveFiles
```
