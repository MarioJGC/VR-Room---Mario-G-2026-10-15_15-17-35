# VR Room – Mario G

Aplicación de realidad virtual para **Meta Quest 3** hecha con **Unity + OpenXR + XR Interaction Toolkit** (proyecto de SGI).
Un apartamento de dos plantas con azotea, en medio de una pequeña ciudad, donde el usuario puede moverse, reorganizar los muebles e interactuar con objetos y aparatos.

---

## 1. Requisitos

| Requisito | Versión / detalle |
|---|---|
| Unity | **6000.5.0f1** (la misma del laboratorio; no actualizar) con *Android Build Support* |
| Git LFS | **Obligatorio**: modelos, texturas y sonidos están en Git LFS |
| Meta Horizon Link | App de Meta para PC, para probar con las gafas por cable o Air Link |
| Gafas | Meta Quest 3 |

## 2. Descargar el proyecto

> **No uses "Download ZIP" de GitHub**: no incluye los archivos de Git LFS y faltarán modelos y texturas.

```bash
git lfs install
git clone https://github.com/MarioJGC/VR-Room---Mario-G-2026-10-15_15-17-35.git
```

Si ya lo clonaste sin Git LFS (salen errores de assets que faltan o modelos invisibles):

```bash
git lfs install
git lfs pull
```

Después abre la carpeta con **Unity 6000.5.0f1** desde Unity Hub y abre la escena **`Assets/Scenes/Mario G Room.unity`**.

## 3. Conectar las gafas (Meta Horizon Link)

Solo la primera vez, en cada PC:

1. Abre **Meta Horizon Link** e inicia sesión con **la misma cuenta de Meta que las gafas**.
2. **Ajustes → General**:
   - Activa **Orígenes desconocidos** (*Unknown Sources*). Sin esto, las gafas muestran *"Please remove your headset and visit settings"*.
   - En **OpenXR Runtime**, pulsa **Set Meta Horizon Link as active** si aparece.
3. Conecta las gafas por USB-C → **Dispositivos → Añadir gafas → Meta Quest 3** y sigue el asistente.

Cada vez que quieras probar:

1. En las gafas: **Ajustes rápidos → Quest Link → Activar**.
2. En Unity: **desactiva `XR Interaction Simulator`** (ver abajo) y pulsa **Play**.

> El botón **"Transmitir"** de Meta Horizon Link aparece desactivado mientras Link está activo. Es normal y no hace falta.

## 4. Probar en el editor

### Con gafas o sin gafas
- **Con gafas (Link):** en la *Hierarchy*, selecciona **`XR Interaction Simulator`** y **desmarca** su casilla en el *Inspector*. Guarda con **Ctrl+S**.
- **Sin gafas (simulador):** vuelve a **marcar** la casilla.

### Controles del simulador
| Tecla | Acción |
|---|---|
| Clic derecho + ratón | Mirar / mover lo que controlas |
| W A S D / Q E | Mover / bajar-subir la cabeza simulada |
| Tab · `[` · `]` · H | Cambiar entre cabeza, mando izquierdo, mando derecho y ambos |
| G | Grip (coger) · **Shift+G** para el mando izquierdo |
| T | Gatillo |
| I J K L | Joystick |
| 1 | Botón X (con el mando izquierdo activo) |
| M | Botón menú (con el mando izquierdo activo) |

> En el simulador se puede "volar" con Q/E porque mueve la cabeza simulada. Con las gafas no pasa.

## 5. Controles en las gafas

```
   MANDO IZQUIERDO                 MANDO DERECHO
   ☰ Menú   → maqueta
   X        → panel de ajustes
   Joystick → andar                Joystick → girar
   Gatillo  → pulsar / usar        Gatillo  → pulsar / usar
   Grip     → coger                Grip     → coger
```

## 6. Funcionalidades

### Navegación
| Funcionalidad | Cómo |
|---|---|
| **Teletransporte** | Apunta al suelo, la escalera, el altillo o la azotea → mantén y **suelta el grip** |
| **Andar** | **Joystick izquierdo** (hacia donde miras). La escalera se sube caminando |
| **Girar** | **Joystick derecho** (por saltos de 45° o continuo, según el panel) |
| **Maqueta (World-in-Miniature)** | Botón **☰ izquierdo** → maqueta sobre la mano. Apunta con el **rayo derecho** (marcador amarillo) → **grip** para teletransportarte ahí. El marcador azul eres tú |
| **Escalera de mano a la azotea** | Ver apartado siguiente |

### Escalera de mano (altillo → azotea)
Está en el altillo, en la pared del fondo, junto a la estantería gris.

1. Acércate a **un brazo de distancia**.
2. Acerca la mano a un peldaño (se ilumina y vibra) → **mantén el grip**.
3. **Tira de la mano hacia abajo** para subir; alterna las manos como en una escalera real.
4. Cuando tu cabeza **asome por la trampilla**, suelta → apareces en la **azotea**.
5. **Para bajar:** apunta con el rayo por la trampilla al suelo del altillo y teletranspórtate, o agarra los peldaños que sobresalen y baja.

### Muebles (sofás, mesas, estanterías, plantas, alfombras…)
| Acción | Cómo |
|---|---|
| Resaltar | Apuntar con el rayo → brillo **azul** + vibración |
| Mover | Mantén el **grip** y mueve el mando: se desliza por el suelo, sin atravesar paredes |
| Acercar / girar | Mientras lo sostienes → **joystick** del mismo mando (solo gira en horizontal) |
| Escalar | **Grip con los dos mandos** sobre el mismo mueble → separa o junta las manos (50 %–200 %) |
| Seleccionar | Al soltarlo queda en **verde** |

### Objetos pequeños (libros, fruta, botella, tablet, pelotas, gorra…)
**Grip** para cogerlos (vienen a la mano) y suéltalos moviendo el brazo para lanzarlos. Suenan al chocar.

### Aparatos
| Aparato | Dónde | Cómo |
|---|---|---|
| TV | Salón | Coge el **mando a distancia** (mesita junto al sofá) → **gatillo** → reproduce vídeo |
| Farol | Estantería bajo el altillo | Cógelo → **gatillo** |
| Lámparas | Salón, bajo el altillo y altillo | Apunta → **grip** |
| Tocadiscos | Mueble bajo el altillo | Apunta → **grip** → música |
| Interruptor | Pared, junto a la puerta | Apunta → **grip** → lámpara de techo del salón |

### Panel de ajustes de navegación
Botón **X** para abrir/cerrar. Se usa apuntando con el rayo y pulsando el **gatillo**; los cambios se aplican y guardan al momento.
- Movimiento continuo (on/off) y velocidad.
- Tipo de giro (por saltos / continuo), ángulo y velocidad.
- Mostrar la maqueta.

## 7. Herramientas del editor (menú `Tools → VR Room`)

La escena se genera con scripts de editor. **Si reconstruyes algo, respeta este orden:**

| # | Menú | Qué hace |
|---|---|---|
| 1 | **Construir apartamento (2 plantas)** | Apartamento, escaleras, azotea, escalera de mano, luces, muebles y objetos |
| 2 | **Configurar navegación** | Movimiento continuo, gravedad, cuerpo del jugador, escalada y maqueta |
| 3 | **Crear panel de ajustes de navegación** | Panel 3D, EventSystem y UI en los rayos |
| 4 | **Construir ciudad alrededor (200 m)** | Calles, edificios, parques y niebla |

- Después de **1**, ejecuta siempre **2** (la maqueta es una copia del apartamento).
- Al reconstruir el apartamento, los muebles y objetos creados por el script vuelven a su posición inicial.
- *Limpiar configuración antigua de la escena* fue una limpieza de una sola vez; no hace falta volver a usarla.

## 8. Problemas frecuentes

| Problema | Solución |
|---|---|
| Faltan assets, modelos invisibles o materiales rosas | Git LFS no estaba instalado al clonar: `git lfs install` + `git lfs pull` |
| *"Please remove your headset and visit settings"* | Activa **Orígenes desconocidos** en Meta Horizon Link (apartado 3) |
| *"Ya se está reproduciendo algo"* / Link no inicia con otra persona | Misma cuenta de Meta en gafas y PC. Antes de cambiar de persona: para **Play**, desconecta Quest Link en el PC anterior y cierra cualquier app abierta en las gafas |
| *"Unauthorized device detected"* en la consola | Mensaje informativo (depuración USB sin autorizar). No afecta a Link; se puede ignorar |
| Me veo demasiado alto o bajo | En `XR Origin (VR)` → **Tracking Origin Mode = Floor**. En las gafas: **Ajustes → Límite → Restablecer altura del suelo** |
| Errores de OpenXR en la consola al dar Play sin gafas (`xrGetSystem`, `ErrorFunctionUnsupported…`) | Normales: no hay gafas conectadas y entra el simulador |
| El build de Android falla con *"only supported on Vulkan"* | *Project Settings → Player → Android → Graphics APIs*: dejar solo **Vulkan** (ya configurado) |
| Avisos amarillos de scripts de `_Course Library` | APIs antiguas del curso; funcionan y se pueden ignorar |

## 9. Estructura del proyecto

```
Assets/
├── _Project/                  ← trabajo propio
│   ├── Scripts/
│   │   ├── Interaction/       muebles, objetos, aparatos, zonas de colocación
│   │   ├── Navigation/        ajustes de locomoción, maqueta, escalera de mano
│   │   ├── UI/                panel de ajustes
│   │   └── Editor/            constructores (Tools → VR Room)
│   └── Materials/             materiales generados
├── Scenes/Mario G Room.unity  ← escena principal
├── _Course Library/           assets del curso "Create with VR" (Unity Learn)
└── Samples/                   XR Interaction Toolkit Starter Assets y simulador
```

## 10. Créditos

- Assets de mobiliario, objetos, audio y vídeo: curso **Create with VR** de Unity Learn (`_Course Library`).
- Escalera de mano basada en la `Ladder` del proyecto **Unity-VR-Basics-2022** de *Fist Full of Shrimp* (licencia CC0), adaptada al sistema de escalada de XR Interaction Toolkit 3.
- Desarrollo asistido con **Claude** (Anthropic): se declara en la *disclosure notice* del documento de entrega.
