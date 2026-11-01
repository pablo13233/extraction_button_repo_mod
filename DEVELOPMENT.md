# Guía de Desarrollo y Compilación

Documentación técnica para compilar y modificar **Extraction Button Confirm**.

## Requisitos

1. **.NET SDK** (8.0 o superior).
2. **R.E.P.O.** instalado en Steam.
3. Perfil de **BepInEx 5** en Thunderstore Mod Manager / r2modman.

## Configuración local

Crea o edita el archivo `Directory.Build.props.user` en la raíz del repositorio (ignorado por Git) con tus rutas locales:

```xml
<Project>
  <PropertyGroup>
    <RepoGameRoot>E:\SteamLibrary\steamapps\common\REPO</RepoGameRoot>
    <BepInExProfileRoot>C:\Users\tu_usuario\AppData\Roaming\Thunderstore Mod Manager\DataFolder\REPO\profiles\TU_PERFIL</BepInExProfileRoot>
    <DeployToProfile>true</DeployToProfile>
  </PropertyGroup>
</Project>
```

## Compilación

```powershell
dotnet build -c Release
```

El ensamblado `ExtractionButtonMod.dll` se generará en `src/ExtractionButtonMod/bin/Release/netstandard2.1/` y se copiará automáticamente a `BepInEx/plugins/ExtractionButtonMod/` dentro de tu perfil.

