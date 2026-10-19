# Decisiones

Lo que fui decidiendo por mi cuenta, con el motivo. Cada entrada es algo que no
estaba previsto en el encargo o que cambió sobre la marcha.

## Dependencias y sus licencias

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| System.Data.OleDb | 9.0.0 | MIT | Índice de Windows Search en el buscador del menú Inicio |

## Fase 1 · La bandeja del sistema viene APAGADA por defecto

**Qué pasa.** ManagedShell 0.0.374 se registra bien como bandeja del shell: su
ventana `Shell_TrayWnd` se crea y queda *antes* que la de Explorer, así que un
`FindWindow("Shell_TrayWnd")` nos encuentra a nosotros. Pero no llega **ni un
solo icono**: medido dos veces, con `AutoStartTrayService` y llamando a
`NotificationArea.Initialize()` a mano (que es quien difunde `TaskbarCreated`).

**Por qué.** En Windows 11 la bandeja de Explorer ya no es el `ToolbarWindow32`
clásico; es XAML. Dos consecuencias: `ExplorerTrayService`, que servía para leer
los iconos que ya estaban registrados antes de arrancar nosotros, devuelve cero
(comprobado: `TB_BUTTONCOUNT` sobre `TrayNotifyWnd/SysPager/ToolbarWindow32` ni
siquiera encuentra la ventana), y las apps que ya tenían su icono puesto no se
vuelven a registrar con nadie mientras Explorer siga vivo y atendiéndolas.
Funcionaría de verdad sustituyendo a Explorer como shell, que es lo que hacen
Cairo Shell y RetroBar, y eso ni se plantea aquí.

**Qué hice.** El módulo queda escrito y funcionando (iconos con tooltip, clic
izquierdo, derecho y doble, animación de entrada y salida y desplegable "^"),
pero:
- `Barra.Bandeja` por defecto **false**.
- El servicio solo se enciende si además pones el módulo `bandeja` en alguna
  zona. Si se encendiera sin el módulo, las apps que sí nos hicieran caso
  dejarían de salir en la barra de Windows sin aparecer en ningún sitio.
- Si lo enciendes y a los 8 segundos no ha llegado ningún icono, lo dice en el log.
- Interruptor en Ajustes → Sistema con la explicación al lado.

**Lo más seguro y reversible**, que es la regla de esta noche: no tocar la
bandeja de Explorer salvo que lo pidas a propósito.

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| ManagedShell | 0.0.374 | MIT (cairoshell) | Recibir los iconos de la bandeja del sistema |
