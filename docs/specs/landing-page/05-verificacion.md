# 05 - Verificación: `landing-page`

Fase 6 del flujo SDD. Verificación independiente de los bloques 2 a 8, hecha el 2026-10-03 sobre el árbol de trabajo (no hay commits de la funcionalidad, ver D-1). No se modificó código de producción (`src/Pizzeria/`). No se ejecutó `dotnet ef`, `dotnet run` ni ningún commit. No se leyeron ni imprimieron valores de user-secrets.

Leyenda: **A** = cubierto por prueba automática, **M** = solo verificable a mano (navegador o lectura), **L** = comprobado en esta fase leyendo el código o los archivos, **X** = sin cubrir o incumplido.

---

## 1. Compilación y pruebas (números reales)

| Comando | Resultado |
|---|---|
| `dotnet build Pizzeria.slnx --no-incremental` | Compilación correcta, **0 advertencias, 0 errores** |
| `dotnet test Pizzeria.slnx` (estado recibido) | **219 superadas, 0 con error, 0 omitidas** (coincide con lo informado por el implementador) |
| `dotnet test Pizzeria.slnx` (con las pruebas añadidas en esta fase) | **266 superadas, 0 con error, 0 omitidas**; ejecutado dos veces seguidas con el mismo resultado |
| `dotnet list Pizzeria.slnx package --vulnerable --include-transitive` | Sin paquetes vulnerables en `Pizzeria` ni en `Pizzeria.Tests` (origen nuget.org) |

### Pruebas añadidas (47 casos, archivos nuevos, sin tocar las existentes)

`tests/Pizzeria.Tests/StartupVerificationTests.cs` (18 casos)
- RF-30: el arranque sin `ConnectionStrings:Default` (nula, vacía o solo espacios) lanza `InvalidOperationException` con el mensaje en español y el comando de user-secrets; con cadena arranca.
- RF-23: en Development con el menú vacío el arranque siembra las 3 pizzas; con el menú no vacío no inserta nada; en Testing, Production y Staging no siembra.
- RF-22: el arranque no aplica migraciones (no aparece `__EFMigrationsHistory`) ni en Development ni en Production.
- RNF-06: en Development la CSP es la estricta más `connect-src 'self' ws: wss:` y nada más; en Production y Staging es exactamente la estricta.
- RNF-09 / RF-05: una excepción no controlada (GET y POST) fuera de Development responde 500 con la página genérica en español, sin traza, sin el texto de la excepción, sin los datos del formulario y con las cabeceras de seguridad.

`tests/Pizzeria.Tests/RequirementGapTests.cs` (29 casos)
- RF-01: `/privacidad` y `/Error` sin textos en inglés de la plantilla.
- RF-02: cada ancla (`menu`, `horarios`, `contacto`) tiene exactamente una sección destino, con menú sembrado y vacío; sin `id` duplicados.
- RF-13: "Sobre nosotros" tiene dos frases, sin imagen, sin cifras ni menciones de años, premios o historia.
- RF-16 (overposting): el único `[BindProperty]` es `ContactInput`, sin `SupportsGet` ni `[BindProperties]`; `ContactInput` solo expone los campos del formulario; un POST con `Id` y `CreatedAtUtc` adicionales los ignora.
- RF-19: `/privacidad` responde 200 y `/Privacy` 404.
- RF-20, RF-21, RF-22: hay una sola migración, `InitialCreate`, con 2 tablas, 1 índice, sin `InsertData` ni SQL crudo; tipos de columna y nulabilidad de las 13 columnas; sin columnas de IP ni agente de usuario; `IsFeatured` con valor por defecto `false`.
- RF-29: valores iniciales de `Business` leídos de `appsettings.json`.
- RF-30 / RNF-07: ningún `appsettings*.json` contiene `ConnectionStrings`, contraseñas ni valores con forma de cadena de conexión.

---

## 2. Checklist `seguridad-web-basica`

| # | Punto | Resultado | Evidencia |
|---|---|---|---|
| 1 | Secretos | **Cumple** | Búsqueda de `password`, `pwd=`, `server=`, `data source=`, `user id=`, `secret`, `apikey`, `token=` y claves privadas en `src/`, `tests/`, `.csproj`, `.slnx`, `appsettings*.json`, `launchSettings.json` y migraciones: solo aparecen el `UserSecretsId` (un GUID, no es secreto), el texto del comando de ayuda (`Program.cs:152`) y la cadena ficticia de pruebas `Server=test.invalid;Database=PizzeriaTests` (`tests/Pizzeria.Tests/TestSupport/PizzeriaWebFactory.cs:28`, sin credenciales). `appsettings.json` solo tiene `Logging`, `AllowedHosts` y `Business`. Historial: un único commit (`f6e6aa7`, plantilla) sin cadenas de conexión. `.gitignore` cubre `bin/`, `obj/`, `.env`, `*.user` y `*.pubxml`; no hay `bin/` ni `obj/` versionados. Los user-secrets viven fuera del repositorio. |
| 2 | Formularios | **Cumple** | Antiforgery: sin `IgnoreAntiforgeryToken` en todo `src/`; el `form` usa el tag helper (`_ContactForm.cshtml:45`) y emite `__RequestVerificationToken`; POST sin token o con token falso responde 400 (pruebas). Validación en servidor: `ModelState.IsValid` antes de guardar (`Index.cshtml.cs:69`). Longitudes de `ContactInput` 80/20/254/1000 iguales a las columnas (`ContactInput.cs:15-30`, `AppDbContext.cs:28-31`, migración). Honeypot `Website` comprobado antes de validar (`Index.cshtml.cs:63`), oculto por clase `.hp`, `tabindex="-1"`, `autocomplete="off"`, contenedor `aria-hidden`. Overposting: la vista enlaza `ContactInput`, que no tiene `Id` ni `CreatedAtUtc`; `ContactService` construye la entidad campo a campo (`ContactService.cs:17-24`). Observación D-3 sobre la expresión regular del teléfono. |
| 3 | Límite de peticiones | **Cumple** | `AddRateLimiter` con limitador global particionado por `RemoteIpAddress` (`Program.cs:25-45`); las peticiones que no son POST van a `GetNoLimiter`; ventana fija, `PermitLimit = 5`, `Window = 10 min`, `QueueLimit = 0`; `RejectionStatusCode = 429` con texto plano en español y `Retry-After`. `UseRateLimiter()` después de `UseRouting()` (`Program.cs:130-133`). Pruebas: el sexto POST da 429 y no guarda; inválidos, honeypot y sin token cuentan; 20 GET seguidos no se limitan. La duración de la ventana solo se comprobó leyendo el código. |
| 4 | Salida segura | **Cumple** | Sin `Html.Raw`, `HtmlString` ni `MarkupString`. Vistas y SVG sin `style=`, `<style>`, `<script>`, manejadores `on*`, `javascript:` ni `iframe`. Prueba de codificación de `<script>` en un POST inválido. |
| 5 | Datos | **Cumple** | Sin `FromSql*`, `ExecuteSql*`, `SqlQuery` ni SQL concatenado en `src/`; la migración no tiene `migrationBuilder.Sql`. Solo LINQ con `AsNoTracking()` y `async`. `ContactMessage` guarda nombre, teléfono, correo, mensaje y fecha; sin IP ni agente de usuario. La IP solo se usa en memoria como clave del limitador (`Program.cs:37`). |
| 6 | Transporte y cabeceras | **Cumple** | `UseHttpsRedirection()` y `UseHsts()` fuera de Development (`Program.cs:121-128`). Middleware de cabeceras al principio de la canalización con `OnStarting` (`Program.cs:104-118`): `nosniff`, `Referrer-Policy`, `X-Frame-Options: DENY` y la CSP exacta de RNF-06, también en estáticos, 400, 429 y 500 (pruebas). Orden: cabeceras, manejo de errores y HSTS, redirección HTTPS, routing, rate limiter, endpoints. Observaciones para el despliegue en D-7. |
| 7 | Errores | **Cumple** | `UseExceptionHandler("/Error")` fuera de Development; `Error.cshtml` es genérica y en español. Probado con excepción real en GET y en POST: 500, sin traza ni texto de la excepción. Los fallos de base de datos en `GET /` y `POST /` se capturan y muestran avisos genéricos (pruebas RF-26 y RF-27). |
| 8 | Logs | **Cumple** | Cinco llamadas a `ILogger` en `src/`: `DbSeeder.cs:51` y `:56` (texto fijo y tipo de excepción), `Index.cshtml.cs:65` (texto fijo), `:82` (solo tipos de excepción) y `:121` (excepción del menú, datos públicos). Ninguna recibe `Name`, `Phone`, `Email`, `Message` ni el objeto. Sin `EnableSensitiveDataLogging`. Pruebas con un proveedor de logs que captura todo: ni el envío correcto ni el fallido dejan datos personales. |
| 9 | Dependencias | **Cumple** | `dotnet list package --vulnerable --include-transitive`: sin hallazgos. |
| 10 | Base de datos (usuario no administrador en producción) | **No verificable ahora** | El despliegue está fuera de alcance. Queda como comprobación obligatoria de la funcionalidad de despliegue. |

---

## 3. Desviaciones declaradas por el implementador

| # | Desviación | Evaluación |
|---|---|---|
| a | Nombre del hero con `clamp(2.75rem, 14vw, 9rem)` (`site.css:367`) | **Incumple el literal de RNF-23** (`clamp(3.5rem, 14vw, 9rem)`). Efecto real: por debajo de 400 px el nombre es más pequeño que lo especificado; a 360 px mide 50,4 px en vez de 56 px. A partir de 400 px es idéntico. La razón (que "Harry's" quepa en una línea a 320 px) es válida, pero 320 px no es un ancho exigido (S-9: 360, 768, 1280). Requiere decisión del usuario: aceptar y actualizar RNF-23, o volver a 3.5rem y comprobar a 360 px. Ver D-2. |
| b | Menú vacío muestra el mismo aviso que la base caída (`Index.cshtml:11-20`) | **Aceptable.** Los requisitos no definen qué mostrar con el menú vacío; RF-07 solo pide que no haya sticker y RF-02 que `#menu` exista siempre. Hay prueba. El texto "no está disponible en este momento" es algo impreciso para un menú vacío, pero no contradice ningún requisito. |
| c | Variable extra `--brown-soft: #6B4A3A` (`site.css:34`) | **Aceptable con nota.** RNF-22 lista las seis variables obligatorias (están todas, con los valores exactos) y no prohíbe otras; la propuesta 7.3 pide la descripción del menú "en gris marrón" y RNF-12 prevé medir ese color. Contraste real 7,03:1 sobre crema. Conviene anotar el token en la propuesta 7.1. |
| d | CSP en Development con `connect-src 'self' ws: wss:` (`Program.cs:98-102`) | **Cumple** RNF-06 (lo permite expresamente). Ahora con prueba: solo se añade en Development y no aparece en Production ni Staging. |
| e | Cookie antiforgery con `SecurePolicy = SameAsRequest` (`Program.cs:17`) | **Aceptable.** Es más estricta que el valor por defecto del framework (`None`) y permite el perfil local `http`. Con HTTPS la cookie sale `Secure`. Para el despliegue detrás del proxy de Azure se recomienda `Always` fuera de Development junto con `ForwardedHeaders` (D-7). |
| f | "Escríbenos por WhatsApp" como texto y no como enlace en los avisos | **Cumple.** RF-26 y RF-27 fijan el texto literal y no piden enlace; los enlaces de WhatsApp de cabecera, hero, pie y barra fija siguen presentes (prueba). Mejora opcional de usabilidad: convertirlo en enlace. |

---

## 4. Contrastes calculados (fórmula WCAG 2.x de luminancia relativa)

Combinaciones de texto que realmente aparecen en `site.css`:

| Texto sobre fondo | Dónde se usa | Razón | AA texto normal (4,5) | AA texto grande (3,0) |
|---|---|---|---|---|
| `#6B4A3A` (brown-soft) sobre `#FFF1D0` (cream) | Descripciones del menú, ayuda del campo "Mensaje" | **7,03:1** | Cumple (también AAA) | Cumple |
| `#3B1F14` (brown) sobre cream | Texto base, tarjetas, panel del formulario | 13,47:1 | Cumple | Cumple |
| `#000000` sobre cream | Texto de los campos, botón claro | 18,74:1 | Cumple | Cumple |
| `#9E1410` (red-dark) sobre cream | Errores del formulario | 7,32:1 | Cumple | Cumple |
| brown sobre `#FFC72C` (yellow) | Sección de destacadas, tiques, aviso del menú | 9,67:1 | Cumple | Cumple |
| black sobre yellow | Botón principal, stickers, sello, "Mensaje enviado" | 13,46:1 | Cumple | Cumple |
| cream sobre `#C8201A` (red) | Eslogan, botón secundario, título de contacto | 5,10:1 | Cumple | Cumple |
| yellow sobre red | Solo el nombre del hero (44 px o más) | 3,66:1 | **No llega** | Cumple |
| cream sobre brown | Cabecera, "Sobre nosotros" | 13,47:1 | Cumple | Cumple |
| cream sobre black | Pie | 18,74:1 | Cumple | Cumple |

Conclusión: **todas las combinaciones de texto usadas cumplen AA.** La única que no llega a 4,5:1 es amarillo sobre rojo (3,66:1), usada solo en el titular grande del hero, como exige RNF-12. No se usa rojo sobre amarillo (3,66:1) ni rojo sobre marrón (2,64:1) para texto.

Elementos no textuales: el contorno negro del foco sobre marrón da 1,39:1 y sobre negro no se ve, pero va siempre acompañado del anillo amarillo de 3 px (9,67:1 sobre marrón, 13,46:1 sobre negro). Sobre amarillo el anillo amarillo no se ve y el contorno negro sí. Falta confirmarlo a la vista (sección 7).

---

## 5. Cobertura de requisitos

### 5.1 Funcionales

| Req. | Estado | Notas |
|---|---|---|
| RF-01 | A | `LandingPageTests`, más `/privacidad` y `/Error` (nuevo) |
| RF-02 | A + M | Anclas y destinos (nuevo). M: visibilidad a 360 y 1280 px |
| RF-03 | A | Pie con y sin redes |
| RF-04 | L | `wwwroot/` solo tiene `css/`, `fonts/`, `img/` y `favicon.svg`; parciales de la plantilla borrados |
| RF-05 | A | Página directa y con excepción real (nuevo) |
| RF-06 | A + L | Comentario `@* TEXTO DE EJEMPLO *@` en `_Hero.cshtml:7` (L) |
| RF-07 | A | Unitarias e integración |
| RF-08 | A + M | M: barra fija a 360 px, oculta a 1280 px, no tapa el pie |
| RF-09 | A | |
| RF-10 | A | |
| RF-11 | A | |
| RF-12 | A | |
| RF-13 | A + L | Dos frases, sin imagen, sin cifras (nuevo). Comentario en `_About.cshtml:6` (L) |
| RF-14 | A | |
| RF-15 | A | |
| RF-16 | A | Marcado, y enlace de `ContactInput` y overposting (nuevo) |
| RF-17 | A | |
| RF-18 | A | Tabla completa en unitarias; 8 casos de extremo a extremo |
| RF-19 | A + M | Ruta (nuevo). M: revisión del texto legal por el usuario |
| RF-20 | A | Metadatos del modelo y columnas de la migración (nuevo) |
| RF-21 | A | Igual que RF-20 |
| RF-22 | A + M | Una migración, sin `InsertData`, sin `Migrate()` al arrancar (nuevo). M: aplicarla en local a mano |
| RF-23 | A + L | Seeder y arranque por entorno (nuevo). Comentarios `// EJEMPLO` en `DbSeeder.cs:29,37,44` (L) |
| RF-24 | A | |
| RF-25 | A | |
| RF-26 | A | |
| RF-27 | A | |
| RF-28 | L + M | `EnableRetryOnFailure(3, 5 s, null)` en `Program.cs:69`. M: con la base local detenida, `GET /` responde 200 sin colgarse |
| RF-29 | A | Valores iniciales (nuevo) y cambio de nombre por configuración |
| RF-30 | A | Fallo de arranque y ausencia de cadena en `appsettings*.json` (nuevo) |

### 5.2 No funcionales

| Req. | Estado | Notas |
|---|---|---|
| RNF-01 | A | |
| RNF-02 | A | |
| RNF-03 | A | |
| RNF-04 | A + M | M: el campo no se ve ni recibe foco en el navegador |
| RNF-05 | A + L | 5 por ventana, 429, GET sin límite (A). Los 10 minutos y la partición por IP, leídos en el código |
| RNF-06 | A + M | Cabeceras y CSP por entorno (nuevo). M: consola del navegador sin violaciones en Production local |
| RNF-07 | L + A | Sección 2, punto 1 |
| RNF-08 | A + L | |
| RNF-09 | A | Sin `Html.Raw` (L), codificación y página de error real (nuevo) |
| RNF-10 | L | HTTPS y HSTS presentes; sin paquetes vulnerables; checklist repasado en este documento |
| RNF-11 | L | Sin acciones sobre producción ni fuera del directorio en esta fase |
| RNF-12 | L + M | Razones calculadas en la sección 4; todas cumplen. M: confirmación con herramienta sobre la página pintada |
| RNF-13 | A + M | `label`, "Error:", `aria-describedby`, `role="status"` y `role="alert"`. M: lector de pantalla |
| RNF-14 | A | |
| RNF-15 | M | El CSS define el foco (`site.css:107-111`, `:240`); hay que recorrerlo con teclado |
| RNF-16 | M | Regla presente (`site.css:907-918`) |
| RNF-17 | A + M | M: pestaña de red |
| RNF-18 | L + M | Dos `woff2`, 111.640 bytes en total (109 KB, bajo el límite de 150 KB), `font-display: swap`, solo Bowlby One precargada, licencias OFL presentes. M: que se pinten á, é, í, ó, ú, ñ, ¿ y ¡ con ambas fuentes |
| RNF-19 | A + L | Solo dos SVG propios, sin estilos en línea |
| RNF-20 | A + M | Sin `<script>`, una hoja de estilos, 3 consultas de lectura con `AsNoTracking()`. M: probar con JavaScript desactivado |
| RNF-21 | L + M | Móvil primero con `min-width`, `viewport`, ancho máximo 1100 px. M: 360, 768 y 1280 px sin scroll horizontal; áreas táctiles de 44 px |
| RNF-22 | L | Seis variables exactas; sin transparencias; el único degradado es la franja de cuadros; `--red-dark` solo en `.field__error` y `.form-alert`; fondos por sección correctos. Variable extra `--brown-soft` (desviación c) |
| RNF-23 | **X (parcial)** | Incumple el `clamp` del nombre del hero (D-2). El resto cumple leyendo el CSS: pilas, escala, Bowlby One a 22 px o más y peso 400, `font-synthesis: none`, `tabular-nums`, sin `uppercase`. M: líneas de 65 caracteres en el formulario (D-6) |
| RNF-24 | L + M | Sin sombras con desenfoque; radios 12 px, 8 px y círculo; franja dos veces (A). Observación D-4 sobre sombras de 4 px |
| RNF-25 | M | |
| RNF-26 | M | Una sola animación (`stamp`, 400 ms) y desplazamiento de 2 px en `.btn:active`, leídos en el CSS |
| RNF-27 | L | Sin `DateTime.Now`/`UtcNow`, `.Result`, `.Wait()`, SQL crudo; inyección por constructor; nombres en inglés y textos en español |
| RNF-28 | A + L | SQLite en memoria; sin red. Nota: las pruebas nuevas que arrancan en Development hacen que el host cargue user-secrets si existen, pero el resultado no depende de ellos (la cadena ficticia y SQLite los sustituyen) |
| RNF-29 | A | Existen todas las clases y casos listados; cada prueba con POST usa su propia fábrica |
| RNF-30 | **X (parcial)** | Build y pruebas en verde, pero no hay commits por bloque (D-1) |

---

## 6. Defectos y observaciones, por gravedad

No se encontraron defectos críticos ni altos. Ningún hallazgo de seguridad bloquea la entrega en local.

### Media

**D-1. La funcionalidad no está en commits (RNF-30).** `git log` solo tiene `f6e6aa7` (plantilla). Todos los bloques están sin confirmar en el árbol de trabajo (`src/Pizzeria/Data/`, `Models/`, `Services/`, `Migrations/`, `tests/`, `docs/` y `.claude/` aparecen como no rastreados). No se puede comprobar "un commit por bloque de menos de 400 líneas" y un descarte accidental perdería todo el trabajo. Es un defecto de proceso, no de código. Acción: que el usuario decida cómo confirmar (por bloques, como pide el plan).

### Baja

**D-2. `src/Pizzeria/wwwroot/css/site.css:367`** - `font-size: clamp(2.75rem, 14vw, 9rem)` no coincide con RNF-23 (`clamp(3.5rem, 14vw, 9rem)`). Ver desviación (a). Acción: decisión del usuario y, según la decisión, corregir el CSS o actualizar el requisito.

**D-3. `src/Pizzeria/Models/ContactInput.cs:20`** - La expresión `^\+?[0-9 ]{7,20}$` es la que fija RF-18, pero acepta teléfonos casi sin dígitos: `"1      "` (un dígito y seis espacios) o `"+       "` la cumplen, se guardan recortados como `"1"` o `"+"` y además satisfacen la regla "teléfono o correo". Comprobado sobre la expresión regular; no se probó de extremo a extremo. No es un fallo de seguridad (longitud acotada, salida codificada), pero deja mensajes sin forma de responder. Es un defecto del requisito, no de la implementación. Acción sugerida: exigir al menos 7 dígitos (cambio de RF-18 y de sus pruebas).

**D-4. `src/Pizzeria/wwwroot/css/site.css:269` y `:446-458`, `:730-741`** - RNF-24 pide sombra `6px 6px 0 #000` en botones y stickers. El botón pequeño de la cabecera usa `4px 4px 0`, y el sello "Destacada" y el aviso "Mensaje enviado" llevan borde sin sombra. (La sombra de 4 px en `.btn:active`, línea 246, es el efecto de pulsado de RNF-26 y es correcta.) Acción: decisión de diseño; aceptar o igualar.

**D-5. `src/Pizzeria/Pages/Error.cshtml.cs:10-17`** - Quedan `RequestId`, `ShowRequestId` y la lectura de `Activity.Current` de la plantilla; la vista ya no los muestra. Código muerto. Acción: eliminar.

**D-6. `src/Pizzeria/Pages/Shared/_ContactForm.cshtml:103`** - La clase `form__legal` no tiene regla en `site.css`. No rompe nada, pero ese párrafo y `form__hint` (línea 58) no tienen `max-width: 65ch`; en escritorio el panel deja líneas de unos 70 caracteres (RNF-23). Acción: añadir la regla o quitar la clase.

### Informativas (para la funcionalidad de despliegue, fuera de alcance)

**D-7.**
- `Program.cs:37`: detrás del proxy de Azure todas las peticiones comparten IP; sin `ForwardedHeaders`, cinco envíos de cualquier visitante bloquean el formulario diez minutos para todos. Ya anotado en la propuesta.
- `Program.cs:17`: usar `CookieSecurePolicy.Always` fuera de Development.
- `appsettings.json:8`: `AllowedHosts: "*"`; restringir al dominio real.
- `Program.cs:125`: HSTS con el valor por defecto de 30 días.
- `.gitignore` no ignora `appsettings.Production.json`; no hace falta mientras los secretos vayan en App Settings, pero conviene tenerlo presente.
- Checklist punto 10: usuario de base de datos sin permisos de administrador.
- `Index.cshtml.cs:121` registra la excepción completa del menú; no contiene datos personales, aunque el mensaje del proveedor puede nombrar el servidor. Es coherente con RF-26.

---

## 7. Comprobaciones manuales pendientes

No se abrió el navegador ni se ejecutó la aplicación en esta fase. Queda por hacer:

1. **Anchos 360, 768 y 1280 px** (RF-02, RF-08, RNF-21): sin scroll horizontal; cabecera reducida en móvil; barra fija de WhatsApp en móvil y oculta en escritorio; el pie se lee completo con la barra visible; destacadas en una y dos columnas; áreas táctiles de 44 px.
2. **Nombre del hero a 360 px** con el `clamp` actual y con el de RNF-23, para decidir D-2.
3. **Teclado y foco** (RNF-15): orden de tabulación, foco visible sobre rojo, marrón, amarillo, crema y negro; el honeypot no recibe foco; la barra fija no tapa el elemento enfocado.
4. **Movimiento** (RNF-16, RNF-26): animación del sticker al cargar; sin animación con `prefers-reduced-motion`.
5. **CSP en el navegador** (RNF-06): consola sin violaciones en Production local y recarga en caliente funcionando en Development.
6. **Pestaña de red** (RNF-17): solo peticiones al propio origen; la fuente Bowlby One se descarga una vez.
7. **Fuentes** (RNF-18): á, é, í, ó, ú, ñ, ¿ y ¡ pintadas con Bowlby One y con Archivo, sin caer a la fuente de respaldo.
8. **Contraste con herramienta** sobre la página pintada (RNF-12), para confirmar los valores de la sección 4.
9. **Lector de pantalla** (RNF-13): anuncio de "Mensaje enviado" y de los errores.
10. **JavaScript desactivado** (RNF-20): la página y el formulario funcionan.
11. **Base de datos real** (RF-22, RF-28): revisar y aplicar `InitialCreate` en local con `dotnet ef database update`; con la base detenida, `GET /` responde 200 con el aviso en unos segundos.
12. **Lenguaje visual** (RNF-24, RNF-25): tiques con muescas, sticker sobre la pizza, tablero de precios, decisión sobre D-4.
13. **Contenido pendiente del usuario**: texto del aviso de privacidad y nombre legal (`Business:LegalName`), eslogan, "Sobre nosotros" y descripciones de las pizzas.
