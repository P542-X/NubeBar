; Instalador de NubeBar (Inno Setup 6).
;
; Se compila en el workflow de Release, que le pasa la versión de la etiqueta:
;   ISCC /DAppVersion=1.1.39 /O"dist" installer\NubeBar.iss
;
; Dos decisiones que no son el valor por defecto y que vale la pena explicar:
;
; 1. POR USUARIO, SIN UAC. app.manifest no pide administrador: NubeBar se las
;    apaña con permisos normales (esconder la barra, la bandeja, los atajos).
;    Así que se instala en %LOCALAPPDATA%\Programs\NubeBar y no sale ninguna
;    ventana de "¿permites que esta app haga cambios?". Pedir admin para luego
;    no necesitarlo solo sirve para asustar.
;
; 2. NADA DE AppMutex: la cierra este script a mano. Lo primero que se probó fue
;    AppMutex con el mutex que ya tiene Instancia.cs, y parecía lo elegante. Pero
;    AppMutex solo sabe PLANTARSE: desinstalar con NubeBar abierta daba un bonito
;    código de salida 1 y no desinstalaba nada. Lo que hace falta no es detectarla,
;    es cerrarla — ver CerrarNubeBar más abajo.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B7E4F2A1-9C3D-4E58-A0F6-2D1B8C5E7A94}
AppName=NubeBar
AppVersion={#AppVersion}
AppVerName=NubeBar {#AppVersion}
AppPublisher=P542-X
AppPublisherURL=https://github.com/P542-X/NubeBar
AppSupportURL=https://github.com/P542-X/NubeBar/issues
AppUpdatesURL=https://github.com/P542-X/NubeBar/releases
VersionInfoVersion={#AppVersion}

; Sin admin: se instala para el usuario que lo ejecuta
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\NubeBar
DefaultGroupName=NubeBar
DisableProgramGroupPage=yes
DisableDirPage=auto

; NubeBar es de 64 bits (PlatformTarget x64 en el csproj)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; La cierra CerrarNubeBar (sección [Code]), no el Restart Manager: las islas son
; ventanas WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW y no hay por qué fiarse de que las
; encuentre
CloseApplications=no
RestartApplications=no

; Relativo a ESTE fichero, que vive en installer\: el instalador sale en dist\,
; al lado del exe que envuelve
OutputDir=..\dist
OutputBaseFilename=NubeBar-Setup
UninstallDisplayName=NubeBar
UninstallDisplayIcon={app}\NubeBar.exe
; El exe ya viene comprimido de fábrica (EnableCompressionInSingleFile), así que
; apretarlo otra vez al máximo cuesta minutos y no quita ni un mega: /fast
Compression=lzma2/fast
SolidCompression=no
WizardStyle=modern

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Marcada de serie a propósito: NubeBar sustituye a la barra de tareas, y sin
; arrancar con Windows te quedas sin barra en el siguiente reinicio hasta que te
; acuerdes de abrirla a mano. Se puede quitar después en Ajustes > General.
Name: "arranque"; Description: "Arrancar NubeBar al iniciar Windows"; GroupDescription: "Al terminar:"
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Al terminar:"; Flags: unchecked

[Files]
Source: "..\dist\NubeBar.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\NubeBar"; Filename: "{app}\NubeBar.exe"
Name: "{group}\{cm:UninstallProgram,NubeBar}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NubeBar"; Filename: "{app}\NubeBar.exe"; Tasks: escritorio
; El mismo NubeBar.lnk que crea y quita el interruptor de Ajustes > General
; (Arranque.cs), para que los dos hablen del mismo acceso directo
Name: "{userstartup}\NubeBar"; Filename: "{app}\NubeBar.exe"; Tasks: arranque

[Run]
Filename: "{app}\NubeBar.exe"; Description: "Abrir NubeBar ahora"; Flags: nowait postinstall skipifsilent

[Code]

// Cerrar NubeBar antes de tocar su exe. Importa más de lo que parece: NubeBar
// esconde la barra de tareas de Windows mientras vive y la devuelve al cerrarse.
// Quitarla de en medio a lo bruto y ya está dejaría al usuario sin ninguna barra.
//
// Por eso van dos pasadas:
//  1. taskkill SIN /F manda WM_CLOSE, que es como pulsar "Salir de NubeBar": se
//     ejecuta su cierre limpio y las barras de Windows vuelven solas.
//  2. Con /F, para lo que no tiene ventana y no se entera del WM_CLOSE: el
//     guardián, que es el mismo exe con otro papel. Y si el cierre bueno hubiera
//     fallado, matarlo es justo lo que el guardián está esperando para restaurar
//     las barras él (Guardian.cs), así que la red de seguridad sigue puesta.
procedure CerrarNubeBar();
var
  Codigo: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im NubeBar.exe',
       '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  Sleep(2500);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im NubeBar.exe /f',
       '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  Sleep(500);
end;

// Instalar encima de una NubeBar en marcha: si no, el exe está bloqueado
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CerrarNubeBar();
  Result := '';
end;

// Y al desinstalar, lo mismo y por el mismo motivo
function InitializeUninstall(): Boolean;
begin
  CerrarNubeBar();
  Result := True;
end;
