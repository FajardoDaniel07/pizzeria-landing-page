# 02 - Propuesta: `landing-page`

Fase 2 del flujo SDD. Basada en `01-exploracion.md`, `CLAUDE.md`, `docs/brief.md` y los skills `aspnet-efcore-sqlserver` y `seguridad-web-basica`. No se modificó código.
**Aprobada por el usuario el 2026-10-03** con las recomendaciones de las decisiones 1 a 5 (sección 3.2), fuentes alojadas en el sitio y barra fija de WhatsApp en móvil (sección 7.6). La decisión 6 (entorno) quedó verificada: SDK 10.0.401, `dotnet-ef` 10.0.12 y `ConnectionStrings:Default` presente en user-secrets.

---

## 1. Alcance

### Entra
- Una sola página (`/`) con: hero con llamadas a la acción, pizzas destacadas, menú por categorías, horarios y ubicación, formulario de contacto.
- Persistencia con EF Core + SQL Server: `MenuItem` (lectura) y `ContactMessage` (escritura). Cadena en `ConnectionStrings:Default` desde user-secrets.
- Migración inicial y datos de ejemplo del menú solo en Development.
- Limpieza de la plantilla: textos en español, `lang="es"`, CSS propio, sin Bootstrap ni jQuery.
- Seguridad básica del formulario y cabeceras (checklist de `seguridad-web-basica`).
- Proyecto de pruebas `tests/Pizzeria.Tests/` (xUnit).

### Decisión del usuario: "Sobre nosotros" y pie con redes
El brief los pide; el encargo no los menciona. **Recomendación: incluir ambos.** Son HTML estático (unas 30 líneas más CSS), sin datos ni lógica, y sin ellos la página queda incompleta frente al brief. Se entregan con texto de ejemplo y con las redes ocultas hasta que existan (ver sección 3).

### Fuera
- Pagos, carrito, panel de administración (brief).
- Entidad de reservas con fecha/hora/personas; consulta de mensajes desde la web; envío de correos o notificaciones.
- Mapa incrustado, fotos reales, logo gráfico, tipografías servidas por terceros (las fuentes van alojadas en el sitio, sección 7.2).
- Despliegue a Azure y migraciones contra producción (otra funcionalidad, con confirmación explícita).
- Caché del menú, internacionalización, analítica.

---

## 2. Enfoque técnico

### 2.1 Estructura de archivos (todo bajo `src/Pizzeria/`)
| Ruta | Contenido |
|---|---|
| `Models/MenuItem.cs`, `Models/MenuCategory.cs` | Entidad y enum de categoría |
| `Models/ContactMessage.cs` | Entidad |
| `Models/ContactInput.cs` | Modelo de entrada del formulario (DataAnnotations); la vista nunca enlaza la entidad |
| `Models/BusinessInfo.cs` | Opciones del negocio (nombre, teléfono, WhatsApp, dirección, horarios, redes) |
| `Data/AppDbContext.cs` | `DbSet<MenuItem>`, `DbSet<ContactMessage>`, Fluent API |
| `Data/DbSeeder.cs` | Datos de ejemplo, solo Development |
| `Services/MenuService.cs` | Lectura del menú (`AsNoTracking`), agrupado por categoría y destacadas |
| `Services/ContactService.cs` | Convierte `ContactInput` en `ContactMessage` y guarda |
| `Services/PriceFormatter.cs` | Formato COP |
| `Pages/Index.cshtml(.cs)` | `OnGetAsync` y `OnPostAsync` |
| `Pages/Shared/_Hero.cshtml`, `_Featured.cshtml`, `_Menu.cshtml`, `_About.cshtml`, `_HoursLocation.cshtml`, `_ContactForm.cshtml` | Un parcial por sección |
| `Pages/Shared/_Layout.cshtml` | Cabecera con anclas, pie, `lang="es"` |
| `Pages/Privacy.cshtml` | Pasa a ser aviso de tratamiento de datos en `/privacidad` |
| `Pages/Error.cshtml` | Texto genérico en español |
| `wwwroot/css/site.css` | CSS propio, móvil primero, variables de la paleta |
| `wwwroot/img/pizza-placeholder.svg`, `wwwroot/favicon.svg` | Marcadores dibujados a mano (sin licencias de terceros) |

El formulario vive en `Index` (no en una página aparte): el `form` usa `asp-fragment="contacto"` para que, con errores, la página vuelva a la sección del formulario.

Los datos del negocio (no son secretos) van en la sección `Business` de `appsettings.json` y se enlazan a `BusinessInfo` con el patrón de opciones. Así la dirección o las redes se cambian sin tocar código.

### 2.2 Modelo de datos

**`MenuItem`**
| Propiedad | Tipo C# | SQL | Reglas |
|---|---|---|---|
| `Id` | `int` | `int` identity | PK |
| `Name` | `string` | `nvarchar(80)` | Obligatorio |
| `Description` | `string?` | `nvarchar(300)` | Opcional |
| `Price` | `decimal` | `decimal(10,2)` | `HasPrecision(10, 2)`; mayor o igual a 0 |
| `Category` | `MenuCategory` | `nvarchar(20)` | Enum `Pizza, Starter, Drink, Dessert` con `HasConversion<string>()` (legible en la base) |
| `ImagePath` | `string?` | `nvarchar(200)` | Ruta relativa en `wwwroot/img`; nulo usa el marcador |
| `IsFeatured` | `bool` | `bit` | Por defecto `false` |

Índice sobre `Category`. Orden de salida: categoría y luego nombre.

**`ContactMessage`**
| Propiedad | Tipo C# | SQL | Reglas |
|---|---|---|---|
| `Id` | `int` | `int` identity | PK |
| `Name` | `string` | `nvarchar(80)` | Obligatorio |
| `Phone` | `string?` | `nvarchar(20)` | Opcional |
| `Email` | `string?` | `nvarchar(254)` | Opcional |
| `Message` | `string` | `nvarchar(1000)` | Obligatorio |
| `CreatedAtUtc` | `DateTime` | `datetime2(0)` | UTC, asignado en el servidor con `TimeProvider` |

Cambio frente al modelo inicial de `CLAUDE.md`: "teléfono o correo" se separa en dos columnas opcionales para poder validar cada una. No se guarda IP ni agente de usuario (mínimos datos personales).

**`ContactInput`** (validación en servidor, mensajes en español)
- `Name`: `[Required]`, `[StringLength(80, MinimumLength = 2)]`
- `Phone`: `[StringLength(20)]`, `[RegularExpression(@"^\+?[0-9 ]{7,20}$")]`
- `Email`: `[EmailAddress]`, `[StringLength(254)]`
- `Message`: `[Required]`, `[StringLength(1000, MinimumLength = 5)]`
- `Website`: campo trampa (honeypot), sin validación
- `IValidatableObject`: exige al menos uno entre `Phone` y `Email`
- Los valores se recortan (`Trim`) antes de guardar.

### 2.3 `AppDbContext` y `Program.cs`
- `AddDbContext<AppDbContext>` con `UseSqlServer(cs, o => o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null))`. Se limita el reintento para que una base caída no deje la página colgada.
- Si `ConnectionStrings:Default` falta o está vacía: `InvalidOperationException` al arrancar con un mensaje que indica el comando de user-secrets. Nunca hay valor por defecto en código ni en `appsettings*.json`.
- Registro de `MenuService`, `ContactService`, `TimeProvider.System` y `Configure<BusinessInfo>`.
- Las migraciones no se aplican solas al arrancar: se usa `dotnet ef database update` a mano en local.

### 2.4 Migración y seed
- Una migración `InitialCreate` con las dos tablas (se revisa el archivo generado antes de aplicarla).
- Seed con `DbSeeder` y no con `HasData`: `HasData` queda dentro de la migración y llegaría a producción, lo que el skill prohíbe sin aviso. El seeder se ejecuta al arrancar solo si `IsDevelopment()` y la tabla `MenuItems` está vacía; es idempotente. Si la base no responde, registra un aviso y la app arranca igual.

### 2.5 Precios y fechas
- Precio guardado como `10500` (`decimal`). Se muestra con `PriceFormatter.ToCop(price)`, que usa `CultureInfo("es-CO")` y formato `N0` de forma explícita: `$10.500`. No depende de la cultura del servidor.
- Fechas en UTC (`CreatedAtUtc`). La landing no muestra fechas, así que no hay conversión a hora de Medellín en esta funcionalidad.

### 2.6 Bootstrap y jQuery
**Se retiran.** Se elimina `wwwroot/lib/` completo, `_ValidationScriptsPartial.cshtml`, `_Layout.cshtml.css` y el `<script type="importmap">` del layout. Sustitutos:
- Maquetación con CSS propio (Grid/Flexbox, variables para rojo, amarillo, crema, marrón oscuro y negro; contraste AA).
- Validación de cliente con atributos HTML5 (`required`, `maxlength`, `type="email"`, `type="tel"`); la de servidor es la que manda.
- JavaScript: ninguno en principio. `wwwroot/js/site.js` se elimina si queda vacío.
- Tipografía: Bowlby One y Archivo alojadas en `wwwroot/fonts/` (ver sección 7.2), con pila del sistema como respaldo. Sin peticiones a terceros, lo que mantiene la CSP estricta.

### 2.7 Si la base de datos no está disponible
- `OnGetAsync` captura el fallo de `MenuService`, lo registra (sin datos personales) y marca `MenuUnavailable`. Hero, "Sobre nosotros", horarios, pie y botón de WhatsApp se muestran igual; destacadas y menú se sustituyen por "El menú no está disponible en este momento. Escríbenos por WhatsApp."
- `OnPostAsync`: si falla el guardado, se muestra un error general ("No pudimos guardar tu mensaje. Inténtalo de nuevo o escríbenos por WhatsApp.") y se conservan los datos escritos. Sin trazas ni detalles de la base.

### 2.8 Seguridad del formulario
- **Antiforgery:** el de Razor Pages por defecto (tag helper `form`). No se desactiva.
- **Validación en servidor:** `ModelState.IsValid` sobre `ContactInput`; longitudes máximas iguales a las de las columnas.
- **PRG:** tras guardar, `TempData["ContactOk"]` y `RedirectToPage` con fragmento `#contacto`.
- **Honeypot:** campo `Website` oculto por CSS, con `tabindex="-1"`, `autocomplete="off"` y `aria-hidden`. Si llega con valor, se responde como éxito sin guardar.
- **Rate limiting:** `AddRateLimiter` con limitador global particionado por IP que solo cuenta peticiones `POST`: 5 cada 10 minutos, respuesta 429. `UseRateLimiter()` después de `UseRouting()`.
- **Cabeceras** (middleware en línea en `Program.cs`): `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` y CSP `default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`. Sin estilos ni scripts en línea.
- **Salida:** sin `Html.Raw`. **Logs:** nunca nombre, teléfono, correo ni mensaje.
- HTTPS y HSTS ya están en la plantilla. Al final: `dotnet list package --vulnerable` y checklist completo.

---

## 3. Huecos del brief

### 3.1 Valores por defecto (se aplican salvo que digas lo contrario)
| Hueco | Valor propuesto | Dónde se cambia |
|---|---|---|
| Nombre | "Harry's Pizza" con apóstrofo recto | `appsettings.json` (`Business:Name`) |
| Dirección exacta | Se muestra "Medellín, Antioquia". `Business:Address` vacío: no se pinta línea de dirección ni enlace "Cómo llegar". Al rellenarlo aparece un enlace a Google Maps (sin iframe) | `appsettings.json` |
| Horarios | Lunes a jueves 2:00 p. m. a 10:30 p. m.; viernes a domingo 12:00 m. a 10:30 p. m. | `appsettings.json` |
| WhatsApp | `https://wa.me/573113706576?text=Hola, quiero hacer un pedido` (codificado). Se asume +57 | `appsettings.json` |
| Botón "Reservar" | Ancla a `#contacto` | `_Hero.cshtml` |
| Redes | `Business:Social` vacío: el pie muestra nombre, teléfono, WhatsApp y enlace a privacidad, sin iconos de redes | `appsettings.json` |
| Menú | Las 3 pizzas del brief a $10.500 (COP), categoría `Pizza`. Las categorías sin productos no se muestran | `Data/DbSeeder.cs` |
| Descripciones | Una frase corta de ejemplo por pizza, deducida del nombre (por ejemplo "Champiñones y pollo sobre queso mozzarella"), marcada con comentario `// EJEMPLO` en el seeder | `Data/DbSeeder.cs` |
| Imágenes | `ImagePath` nulo: se usa `wwwroot/img/pizza-placeholder.svg` con `alt` igual al nombre de la pizza | `wwwroot/img/` |
| Destacadas | "Carnes" y "Mexicana" (`IsFeatured = true`) | `Data/DbSeeder.cs` |
| "Sobre nosotros" | Dos frases neutras sin datos inventados (ni años, ni premios, ni historia), con comentario Razor `@* TEXTO DE EJEMPLO *@` | `_About.cshtml` |
| Logo | Nombre como texto con estilo de sello en CSS; favicon SVG sencillo | `site.css`, `wwwroot/favicon.svg` |
| Campos de reserva | Ninguno propio. Mensaje libre con texto de ayuda "Para reservar, indica fecha, hora y número de personas" | `_ContactForm.cshtml` |
| Eslogan del hero | Texto de ejemplo marcado con `@* TEXTO DE EJEMPLO *@` | `_Hero.cshtml` |

### 3.2 Decisiones que necesitan tu respuesta
1. **¿Entran "Sobre nosotros" y el pie con redes?** Recomendación: sí, con el texto de ejemplo y las redes ocultas.
2. **¿Se retiran Bootstrap y jQuery?** Recomendación: sí (lo pide la convención de `CLAUDE.md`). Es lo que más condiciona el trabajo de maquetación.
3. **Menú: ¿solo las 3 pizzas, o añado productos inventados en entradas, bebidas y postres?** Recomendación: solo las 3 y ocultar categorías vacías; inventar productos y precios puede confundirse con el menú real.
4. **Formulario: ¿mensaje libre con nombre, y al menos teléfono o correo?** Recomendación: sí; los campos de reserva estructurados quedan para otra funcionalidad.
5. **Página `Privacy`: ¿se convierte en aviso de tratamiento de datos (`/privacidad`)?** Recomendación: sí, corto, enlazado desde el formulario (Ley 1581 de 2012). Necesita que revises el texto y el nombre del responsable.
6. **Entorno local: ¿qué SQL Server usas, está ya `ConnectionStrings:Default` en user-secrets y está instalado `dotnet-ef`?** Recomendación: LocalDB y `dotnet-ef` 10.x global. Sin esto no se puede crear ni aplicar la migración (bloque 2).

---

## 4. Plan de trabajo

Cada bloque es un commit/PR de menos de unas 400 líneas escritas a mano, con `dotnet build` y `dotnet test` en verde.

| # | Bloque | Archivos principales | Pruebas (`tests/Pizzeria.Tests/`) |
|---|---|---|---|
| 0 | **Commits previos, separados de la funcionalidad.** (a) `Pizzeria.csproj` con `UserSecretsId` y paquetes EF Core. (b) `CLAUDE.md`, `.claude/`, `docs/`. Verificar `dotnet --version`, `dotnet build`, `dotnet ef --version` | `src/Pizzeria/Pizzeria.csproj` | Ninguna |
| 1 | **Proyecto de pruebas.** xUnit, referencia al proyecto web, alta en `Pizzeria.slnx`. Paquetes: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.Mvc.Testing` | `tests/Pizzeria.Tests/Pizzeria.Tests.csproj` | Prueba mínima que compila y pasa |
| 2 | **Modelo y contexto.** Entidades, enum, `AppDbContext`, registro en `Program.cs` con fallo claro sin cadena, migración `InitialCreate` (revisada, aplicada solo en local) | `Models/`, `Data/AppDbContext.cs`, `Migrations/`, `Program.cs` | `AppDbContextTests`: el modelo se crea en SQLite en memoria; longitudes y precisión configuradas |
| 3 | **Servicios y seed.** `MenuService`, `ContactService`, `PriceFormatter`, `ContactInput`, `DbSeeder` | `Services/`, `Models/ContactInput.cs`, `Data/DbSeeder.cs` | `PriceFormatterTests` (`10500` da `$10.500`); `ContactInputTests` (obligatorios, longitudes, teléfono o correo); `MenuServiceTests` (agrupa, oculta categorías vacías, destacadas); `ContactServiceTests` (guarda con fecha UTC y recorta); `DbSeederTests` (idempotente) |
| 4a | **Retirar plantilla (mecánico, solo borrados).** `wwwroot/lib/`, `_ValidationScriptsPartial.cshtml`, `_Layout.cshtml.css`, `js/site.js` | Borrados | Las existentes siguen pasando |
| 4b | **Layout y base visual.** `_Layout.cshtml` en español, `BusinessInfo` y sección `Business`, base de `site.css` (variables, tipografía, cabecera, pie), `Error.cshtml` en español, favicon | `Pages/Shared/_Layout.cshtml`, `Models/BusinessInfo.cs`, `appsettings.json`, `wwwroot/css/site.css` | Integración: `GET /` responde 200 con `lang="es"` y el nombre del negocio |
| 5 | **Secciones estáticas.** Hero, "Sobre nosotros", horarios y ubicación, y su CSS | `_Hero.cshtml`, `_About.cshtml`, `_HoursLocation.cshtml` | Integración: el enlace de WhatsApp contiene `wa.me/573113706576`; sin dirección no aparece "Cómo llegar" |
| 6 | **Destacadas y menú.** `OnGetAsync`, parciales, marcador de imagen, aviso si la base no responde | `Index.cshtml(.cs)`, `_Featured.cshtml`, `_Menu.cshtml` | Integración con SQLite: se ven las pizzas sembradas y el precio con formato; con el servicio fallando la página responde 200 con el aviso |
| 7 | **Formulario de contacto.** `OnPostAsync`, PRG, honeypot, mensajes de error y de confirmación | `Index.cshtml.cs`, `_ContactForm.cshtml` | Integración: POST válido guarda y redirige; inválido no guarda y muestra errores; honeypot relleno no guarda; POST sin token antiforgery da 400 |
| 8 | **Seguridad y privacidad.** Rate limiter, cabeceras, página `/privacidad`, checklist de `seguridad-web-basica`, `dotnet list package --vulnerable` | `Program.cs`, `Pages/Privacy.cshtml` | Integración: cabeceras presentes en `GET /`; el sexto POST seguido devuelve 429 |
| 9 | **Repaso final.** Revisión manual en móvil y escritorio, contraste, etiquetas y `alt`, navegación con teclado; ajustes de CSS | `wwwroot/css/site.css` | Suite completa en verde |

Pruebas de integración: `WebApplicationFactory<Program>` sustituyendo el `AppDbContext` por SQLite en memoria y aportando una cadena ficticia por configuración de prueba. No se usa SQL Server en las pruebas.

---

## 5. Alternativas descartadas
- **`HasData` para el menú:** sembraría datos de ejemplo en producción con la migración.
- **Conservar Bootstrap/jQuery:** contradice "CSS propio, JS mínimo" y añade peso; la estética pedida no se parece a Bootstrap.
- **Página `/Contacto` aparte:** más simple de programar, pero el brief pide una landing de una página.
- **Tabla `Category`:** cuatro categorías fijas no justifican una relación; basta un enum.
- **Entidad `Reservation`:** amplía modelo, validación y pruebas; el brief no define sus campos.
- **Mapa incrustado y Google Fonts:** obligan a abrir la CSP y añaden peticiones externas; además no hay dirección exacta.
- **Proveedor InMemory de EF en pruebas:** no aplica restricciones ni longitudes; SQLite en memoria se parece más a SQL Server.
- **reCAPTCHA:** dependencia externa y cookies de terceros; honeypot más rate limiting bastan para este tamaño.
- **Aplicar migraciones al arrancar (`Database.Migrate()`):** arriesga ejecutar migraciones en producción sin confirmación.

---

## 6. Riesgos y supuestos

### Supuestos (sin verificar; se comprueban en el bloque 0)
- SDK de .NET 10 instalado y el proyecto compila.
- `dotnet-ef` instalado y compatible con EF Core 10.0.12. Si falta, se propone `dotnet tool install --global dotnet-ef` (pide tu confirmación, instala fuera del directorio del proyecto).
- Hay un SQL Server local accesible y `ConnectionStrings:Default` está en user-secrets. No se imprimirá la cadena para comprobarlo; se te pedirá confirmación.
- Precios en COP, WhatsApp con +57, nombre con apóstrofo recto.
- En .NET 10 la clase `Program` es accesible desde las pruebas; si no, se añade `public partial class Program { }`.

### Riesgos
- **Bloqueo en el bloque 2** si falta `dotnet-ef`, la instancia o la cadena. Mitigación: los bloques 1, 3 (con SQLite), 4 y 5 no dependen de SQL Server y pueden adelantarse.
- **SQLite no es SQL Server:** no valida precisión decimal ni ordena por `decimal`. Mitigación: no se ordena por precio; la migración real se revisa y se aplica en local a mano.
- **Reintentos con la base caída:** aun limitados, la primera carga puede tardar unos segundos. El tiempo de conexión depende de `Connect Timeout` en la cadena (se recomienda bajo en local).
- **CSP en Development:** la recarga en caliente usa WebSocket y scripts inyectados. Si se bloquean, se añade `connect-src 'self' ws: wss:` solo en Development.
- **Rate limiting por IP detrás del proxy de Azure:** todas las peticiones pueden llegar con la misma IP. Requiere `ForwardedHeaders` al desplegar; queda anotado para la funcionalidad de despliegue.
- **Formato `es-CO`:** depende de los datos ICU del sistema. La prueba de `PriceFormatter` lo detecta; alternativa: formato manual con separador fijo.
- **Contenido de ejemplo visible:** descripciones, eslogan y "Sobre nosotros" son marcadores. Quedan listados en la sección 3.1 para sustituirlos antes de publicar.
- **Bloque 4a con muchas líneas borradas:** supera las 400 líneas de diff, pero son solo eliminaciones de la plantilla; por eso va aislado.
- **Aviso de privacidad:** texto orientativo, no asesoría legal; necesita tu revisión.
- **Sin panel de administración:** los mensajes solo se consultan en la base de datos. Aceptado por el brief.

---

## 7. Dirección de diseño

Elaborada con el skill `frontend-design` a partir del brief: "comercial, juvenil y llamativo", "audaz y ligeramente retro/rústico", "tipografías grandes, gruesas y expresivas, contrastes fuertes, formas dinámicas y elementos tipo stickers, etiquetas, sellos". Validada por el usuario el 2026-10-03.

**Sujeto, público y trabajo de la página.** Una pizzería de barrio en Medellín con precios bajos ($10.500). Público joven que llega desde el móvil. El trabajo de la página es uno: que pedir por WhatsApp esté a un toque desde cualquier punto.

**Idea central.** La página se lee como la tapa de una caja de pizza y el tablero de precios de un local de comida rápida de los años 70: bloques de color plano, letra de cartel, borde negro grueso y sombra dura sin difuminar. Lo memorable es una sola cosa, el hero; el resto se mantiene ordenado y sobrio.

### 7.1 Paleta

Los cinco colores son los que pide el brief. Colores planos, sin degradados ni transparencias.

| Token CSS | Nombre | Hex | Uso |
|---|---|---|---|
| `--red` | Rojo salsa | `#C8201A` | Fondo del hero y de la sección de contacto |
| `--yellow` | Amarillo queso | `#FFC72C` | Fondo de destacadas, botón principal, stickers de precio, título del hero |
| `--cream` | Crema masa | `#FFF1D0` | Fondo del menú y de horarios, paneles, texto sobre rojo y marrón |
| `--brown` | Marrón horno | `#3B1F14` | Texto de lectura, cabecera, fondo de "Sobre nosotros" |
| `--black` | Negro | `#000000` | Bordes, sombras duras, pie |
| `--red-dark` | Rojo error | `#9E1410` | Solo mensajes de error del formulario, sobre crema |

Reglas de uso:
- El rojo y el amarillo mandan; el crema es el papel donde se lee. La página no es "fondo crema con un acento".
- Combinaciones de texto permitidas (contraste estimado; se mide con herramienta en el bloque 9): marrón o negro sobre crema y sobre amarillo (mayor de 10:1); crema o blanco sobre rojo (cerca de 5:1, cumple AA); crema sobre marrón y sobre negro (mayor de 12:1).
- Amarillo sobre rojo ronda 3,7:1: solo para titulares grandes (el nombre en el hero), nunca para texto de lectura ni botones.
- Rojo sobre amarillo y rojo sobre marrón no se usan para texto.

### 7.2 Tipografías

Dos familias, claramente distintas, ambas con licencia SIL Open Font License y alojadas en el propio sitio (`wwwroot/fonts/`, formato woff2). No se carga nada de terceros, así que la CSP no cambia (`default-src 'self'` ya cubre las fuentes).

| Rol | Familia | Por qué |
|---|---|---|
| Títulos, nombre del negocio, precios en stickers | **Bowlby One** (un solo peso) | Letra de cartel muy gruesa, con formas algo irregulares que dan el aire retro y rústico sin recurrir a texturas. Sustituye a `Impact`, que es más fría |
| Lectura, menú, formulario, navegación | **Archivo** (variable: peso y anchura) | Grotesca legible en móvil. Un solo archivo sirve para el texto normal (peso 400), los nombres de pizza (700) y los precios y la navegación en versión condensada (anchura 75, peso 800) |

- Pila de respaldo: `"Bowlby One", Impact, "Arial Black", sans-serif` y `Archivo, system-ui, sans-serif`. Con `font-display: swap`; se precarga solo Bowlby One.
- Escala (proporción 4:3, base 17 px, interlineado 1,5): 17, 22, 30, 40, 53 px. El nombre en el hero usa `clamp(3.5rem, 14vw, 9rem)` con interlineado 0,9.
- Bowlby One nunca se pone en negrita simulada ni por debajo de 22 px.
- Títulos en mayúscula inicial normal ("Pizzas destacadas"), sin bloques en mayúsculas y sin rótulos pequeños encima de cada título.
- Precios con `font-variant-numeric: tabular-nums` para que alineen en columna.
- Texto de lectura alineado a la izquierda, líneas de 65 caracteres como máximo.
- Al descargar las fuentes se comprueba que cubren á, é, í, ó, ú, ñ, ¿ y ¡. Si Bowlby One no las cubre, la alternativa es Alfa Slab One (misma licencia).

### 7.3 Estilo de las secciones

Lenguaje común:
- **Borde y sombra:** borde negro de 3 px y sombra dura `6px 6px 0 #000` en botones, tarjetas, paneles y stickers. Ninguna sombra difuminada.
- **Esquinas:** 12 px en tarjetas y paneles, 8 px en botones, círculo completo en stickers. Tres valores, cada uno para una cosa.
- **Cambio de sección:** por color de fondo, sin líneas finas. Un único motivo gráfico, la franja de cuadros rojo y crema (mantel de pizzería, hecha con CSS), que aparece dos veces: bajo el hero y sobre el pie.
- **Stickers y sellos:** solo donde informan. El sticker circular amarillo lleva un precio; el sello "Destacada" marca esas pizzas dentro del menú completo. Van girados entre 4° y 8°. No se inventan promociones ni descuentos.
- **Composición:** alineada a la izquierda, ancho máximo de 1100 px. La energía viene del tamaño de la letra, el color y los stickers girados, no de centrar todo.

```
Móvil                              Escritorio
+---------------------------+      +-----------------------------------------------+
| Harry's Pizza      [Menú] |      | Harry's Pizza   Menú  Horarios  Contacto  [WA] |
+---------------------------+      +-----------------------------------------------+
| ROJO                      |      | ROJO                                           |
| Harry's                   |      | Harry's               .---------.              |
| Pizza          (Desde     |      | Pizza                /   pizza   \  (Desde     |
|                 $10.500)  |      |                      |  cortada  |   $10.500)  |
| Eslogan en una línea      |      | Eslogan              \ por borde /             |
| [Pedir por WhatsApp]      |      | [Pedir por WhatsApp] [Reservar]                |
| [Reservar]                |      +#################### cuadros ###################+
|      pizza, cortada abajo |      | AMARILLO  Pizzas destacadas                    |
+######## cuadros ##########+      | [ tarjeta grande ]   [ tarjeta grande ]        |
| AMARILLO  destacadas      |      +-----------------------------------------------+
| [ tarjeta ]               |      | CREMA  Menú                                    |
| [ tarjeta ]               |      | Carnes (Destacada) ................... $10.500 |
+---------------------------+      | Champiñón con pollo .................. $10.500 |
| [ Pedir por WhatsApp ]    | fija +-----------------------------------------------+
+---------------------------+
```

| Sección | Fondo | Tratamiento |
|---|---|---|
| Cabecera | Marrón | Nombre a la izquierda en Bowlby One crema; anclas a la derecha; botón amarillo de WhatsApp. En móvil, solo nombre y enlace al menú |
| Hero | Rojo | Nombre enorme en amarillo con sombra dura negra; eslogan en crema; botón principal "Pedir por WhatsApp" (amarillo) y secundario "Reservar" (borde crema). La pizza se sale del borde de la sección y el sticker de precio la pisa |
| Destacadas | Amarillo | Dos tarjetas grandes crema: imagen circular, nombre, una línea de descripción y sticker de precio en la esquina |
| Menú | Crema | Tablero de precios, no tarjetas: por categoría, filas con nombre, línea de puntos y precio; descripción debajo en gris marrón. Las destacadas llevan el sello |
| Sobre nosotros | Marrón | Dos frases; la primera a 30 px en crema. Sin imagen |
| Horarios y ubicación | Crema | Dos etiquetas tipo tique (muescas laterales en CSS): horarios como tabla de días y horas; ubicación con ciudad y, cuando exista, dirección y "Cómo llegar" |
| Contacto | Rojo | Panel crema con el formulario. Etiquetas siempre visibles encima de cada campo, campos con borde negro, foco con contorno amarillo y negro de 3 px. Errores en rojo oscuro con el texto "Error:" delante, sin depender solo del color. Botón "Enviar mensaje"; confirmación "Mensaje enviado" |
| Pie | Negro | Franja de cuadros arriba; nombre, teléfono, WhatsApp y enlace a privacidad en crema |

Movimiento: un solo momento al cargar, el sticker de precio del hero entra "estampado" (escala y giro, 400 ms, solo CSS). Los botones se hunden 2 px al pulsarlos. Nada aparece al hacer scroll. Con `prefers-reduced-motion` no hay animación.

Móvil: barra fija inferior con "Pedir por WhatsApp" (solo CSS), que es el trabajo principal de la página; el contenido deja margen inferior para que no tape nada.

### 7.4 Estilo de las imágenes

Mientras no haya fotos reales:
- Un único SVG propio (`wwwroot/img/pizza-placeholder.svg`): pizza vista desde arriba, ilustración plana con los colores de la paleta, contorno negro grueso y halo crema, como una pegatina troquelada. No es una caja gris: encaja con el resto y no finge ser una foto del producto.
- Mismo dibujo para todas las pizzas y para el hero (allí a gran tamaño y cortado por el borde).
- Favicon: la misma pizza simplificada.

Cuando lleguen las fotos reales (guía para tomarlas o elegirlas):
- Cenital, pizza entera y centrada, sobre madera oscura o fondo liso; luz cálida, sin filtros ni texto encima.
- Formato cuadrado 1:1, recortadas en círculo con borde negro de 3 px mediante CSS, de modo que foto e ilustración ocupan el mismo hueco.
- WebP, 800 px de lado, menos de 120 KB; `width` y `height` declarados, `loading="lazy"` salvo la del hero.
- `alt` con el nombre de la pizza; la ilustración decorativa del hero lleva `alt=""`.

Sin texturas de papel, sin fotos de banco de imágenes y sin iconos decorativos: lo rústico lo aportan el marrón, la letra irregular y la franja de cuadros.

### 7.5 Revisión contra el brief (qué se cambió y por qué)

- **Base de la página:** el primer planteamiento era fondo crema con acentos rojos. Es el aspecto por defecto de cualquier página "cálida" y no transmite la energía que pide el brief. Ahora abren el rojo y el amarillo, y el crema queda para las zonas de lectura.
- **Menú:** de rejilla de tarjetas iguales a tablero de precios en filas. Con 3 productos una rejilla queda vacía, y la lista es lo propio de una pizzería.
- **Stickers:** el brief los pide, pero repartidos por todas partes pierden fuerza. Se limitan a precios y al sello de destacada.
- **Descartado:** una tercera tipografía caligráfica para los stickers, textura de papel, y animaciones de entrada por sección.

### 7.6 Cambios que esta sección introduce en el resto de la propuesta

- **Tipografías (antes: pila del sistema con `Impact`):** ahora dos fuentes alojadas en `wwwroot/fonts/`. Añade dos archivos woff2 (en torno a 100 KB en total) y exige descargarlos una vez durante el bloque 4b. Sin peticiones externas en producción.
- **Sticker "Desde $X" del hero:** muestra el precio más bajo del menú, calculado en `MenuService`. Si el menú no está disponible, el sticker no se pinta. Se prueba en el bloque 6.
- **Barra fija de WhatsApp en móvil:** se añade al bloque 5.
- **Decisiones para el usuario:** (7) ¿fuentes alojadas en el sitio, o pila del sistema sin descargas? Recomendación: alojadas; `Impact` no da el carácter que pide el brief. (8) ¿Barra fija de WhatsApp en móvil? Recomendación: sí.
