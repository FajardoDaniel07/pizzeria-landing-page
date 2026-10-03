# 01 - Exploración: `landing-page`

Fase 1 del flujo SDD. Solo lectura del repositorio; no se modificó código.
Limitación: el explorador no tiene terminal, así que no se ejecutó `dotnet --version`, `dotnet build`, `dotnet ef` ni `dotnet user-secrets list`. Lo marcado como "sin verificar" debe comprobarse en la siguiente fase.

Fuentes leídas: `CLAUDE.md`, `docs/brief.md`, `.claude/skills/aspnet-efcore-sqlserver/SKILL.md`, `.claude/skills/seguridad-web-basica/SKILL.md`, `.claude/skills/sdd/SKILL.md`, `Pizzeria.slnx`, `.gitignore` y todo `src/Pizzeria/` (excepto `bin/`, `obj/` y el contenido de `wwwroot/lib/`).

---

## 1. Estado actual

### 1.1 Repositorio
- Un solo commit (`f6e6aa7`, plantilla inicial). Sin commitear: `src/Pizzeria/Pizzeria.csproj` (modificado), `.claude/`, `CLAUDE.md`, `docs/` (sin seguimiento).
- Solución: `Pizzeria.slnx` (formato XML nuevo). Solo incluye `src/Pizzeria/Pizzeria.csproj` dentro de la carpeta `/src/`.
- `.gitignore`: el estándar de `dotnet new gitignore` (ignora `bin/`, `obj/`, `.env`, `*.pubxml`, `*.mdf`, etc.). No ignora `appsettings*.json`, por lo que cualquier secreto escrito ahí quedaría versionado.
- No existen: `tests/`, `README`, `global.json`, `Directory.Build.props`, `.editorconfig`, `.config/dotnet-tools.json`, `.github/`.
- `docs/specs/` solo contenía `.gitkeep`.

### 1.2 Proyecto web `src/Pizzeria/Pizzeria.csproj`
- SDK `Microsoft.NET.Sdk.Web`, `TargetFramework` = `net10.0`, `Nullable` e `ImplicitUsings` activados.
- `UserSecretsId` ya presente: `183a8b05-00f1-4f61-83b4-a062722a4013` (forma parte del cambio sin commitear).
- Paquetes NuGet ya referenciados (también parte del cambio sin commitear):
  - `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12
  - `Microsoft.EntityFrameworkCore.Design` 10.0.12 (`PrivateAssets=all`)
- Los paquetes están restaurados (`obj/project.assets.json` resuelve EF Core 10.0.12, `Microsoft.Data.SqlClient` 6.1.6, `Azure.Identity` 1.17.1 como transitivos) y existe `bin/Debug/net10.0/`, así que el proyecto se compiló al menos una vez con esos paquetes.

### 1.3 `src/Pizzeria/Program.cs` (plantilla sin cambios)
- Solo `AddRazorPages()`.
- Pipeline: `UseExceptionHandler("/Error")` + `UseHsts()` fuera de Development, `UseHttpsRedirection()`, `UseRouting()`, `UseAuthorization()`, `MapStaticAssets()`, `MapRazorPages().WithStaticAssets()`.
- No hay `AddDbContext`, ni lectura de `ConnectionStrings:Default`, ni `AddRateLimiter`, ni cabeceras de seguridad, ni configuración de cultura.

### 1.4 Configuración
- `src/Pizzeria/appsettings.json`: solo `Logging` y `AllowedHosts: "*"`. Sin `ConnectionStrings` (correcto según las reglas duras).
- `src/Pizzeria/appsettings.Development.json`: `DetailedErrors: true` y `Logging`.
- `src/Pizzeria/Properties/launchSettings.json`: perfiles `http` (5227) y `https` (7178 + 5227), ambos en `Development`.

### 1.5 Páginas (`src/Pizzeria/Pages/`), todas de plantilla
| Archivo | Estado |
|---|---|
| `Index.cshtml` / `Index.cshtml.cs` | "Welcome" en inglés; `IndexModel.OnGet()` vacío y síncrono. A reemplazar por la landing. |
| `Privacy.cshtml` / `Privacy.cshtml.cs` | Marcador en inglés. Decidir: eliminar o convertir en aviso de privacidad real. |
| `Error.cshtml` / `Error.cshtml.cs` | Textos en inglés y párrafo "Development Mode" visible al usuario. A traducir y simplificar. |
| `Shared/_Layout.cshtml` | `lang="en"`, navbar Bootstrap con "Home/Privacy", pie "2026 - Pizzeria - Privacy". Carga Bootstrap CSS, jQuery y Bootstrap JS. Contiene `<script type="importmap"></script>` (tag helper de import map). |
| `Shared/_Layout.cshtml.css` | CSS aislado de plantilla (colores azules, `.footer` con posición absoluta). Genera `~/Pizzeria.styles.css`. |
| `Shared/_ValidationScriptsPartial.cshtml` | Carga jquery-validation y jquery-validation-unobtrusive desde `wwwroot/lib/`. |
| `_ViewImports.cshtml`, `_ViewStart.cshtml` | Estándar; se pueden conservar. |

### 1.6 Estáticos (`src/Pizzeria/wwwroot/`)
- `css/site.css`: estilos de plantilla (tamaño de fuente, foco de Bootstrap, `margin-bottom: 60px` para el pie fijo). A reemplazar.
- `js/site.js`: vacío (solo comentarios).
- `favicon.ico`: el de la plantilla.
- `lib/`: `bootstrap`, `jquery`, `jquery-validation`, `jquery-validation-unobtrusive` (plantilla).
- No hay carpeta de imágenes (`wwwroot/img` o similar), ni logo, ni fotos de pizzas.

### 1.7 Lo que viene de la plantilla y hay que reemplazar o limpiar
- Todo el contenido visible está en inglés y `lang="en"`; la convención exige textos en español (`lang="es"`).
- Bootstrap + jQuery chocan con "CSS propio" y "JavaScript mínimo, sin frameworks pesados" de `CLAUDE.md`. Decisión para la propuesta: retirarlos del layout (y de `wwwroot/lib/`) o conservarlos. Nota: la validación de cliente no intrusiva de ASP.NET depende de jQuery; sin ella queda la validación HTML5 nativa más la del servidor (que es la obligatoria).
- Navbar, pie, `site.css`, `_Layout.cshtml.css`, página `Privacy` y textos de `Error`.
- Título por defecto "Pizzeria" en vez de "Harry's Pizza".

---

## 2. Qué falta

### Datos
- `src/Pizzeria/Data/AppDbContext.cs` (la carpeta `Data/` no existe).
- `src/Pizzeria/Models/MenuItem.cs` y `src/Pizzeria/Models/ContactMessage.cs` (la carpeta `Models/` no existe).
- Registro de `AddDbContext<AppDbContext>` con `UseSqlServer(...)` + `EnableRetryOnFailure()` leyendo `ConnectionStrings:Default`.
- Migraciones (`src/Pizzeria/Migrations/` no existe).
- Datos iniciales del menú (`HasData` o seeder solo en desarrollo).
- Valor de `ConnectionStrings:Default` en user-secrets: sin verificar (el almacén está fuera del repositorio y no se debe leer desde aquí).
- Herramienta `dotnet-ef`: sin verificar. No hay manifiesto local (`.config/dotnet-tools.json`); tendría que estar instalada globalmente.
- Instancia de SQL Server local (LocalDB, Express, contenedor): desconocida.

### Web
- Landing en `Pages/Index.cshtml` con sus secciones, `PageModel` async con consultas `AsNoTracking()`.
- Formulario de contacto: modelo de entrada con DataAnnotations y `[BindProperty]`, `OnPostAsync`, post-redirect-get con mensaje de confirmación, antiforgery (Razor Pages lo aplica por defecto en formularios con tag helpers).
- Honeypot y rate limiting (`AddRateLimiter` + `UseRateLimiter`) para el formulario.
- Cabeceras de seguridad (`X-Content-Type-Options`, `Referrer-Policy`, CSP).
- CSS propio, imágenes, favicon y logo propios.
- Configuración de cultura para el formato de precios (por ejemplo `es-CO`).

### Pruebas
- `tests/Pizzeria.Tests/` (proyecto xUnit) no existe; tampoco está en `Pizzeria.slnx`.
- Falta decidir cómo probar el acceso a datos (SQLite en memoria, proveedor InMemory, o solo lógica sin base de datos). Ningún paquete de pruebas está referenciado.

### Ya resuelto (no hay que hacerlo)
- `UserSecretsId` en el `.csproj`.
- Paquetes `Microsoft.EntityFrameworkCore.SqlServer` y `.Design` 10.0.12.
- HTTPS redirection, HSTS y página de error genérica fuera de Development (en `Program.cs`).

---

## 3. Resumen del brief (`docs/brief.md`)

### Secciones pedidas
1. Hero con llamada a la acción (pedir por WhatsApp / reservar)
2. Pizzas destacadas
3. Menú completo por categorías (pizzas, entradas, bebidas, postres)
4. Sobre nosotros
5. Horarios y ubicación (mapa o dirección)
6. Formulario de contacto / reservas
7. Pie de página con redes

Nota: el encargo de esta funcionalidad menciona hero, destacadas, menú, horarios/ubicación y contacto, pero no "Sobre nosotros" ni el pie con redes, que sí están en el brief. Hay que confirmar si entran en el alcance.

### Datos del negocio
- Nombre: "Harry´s Pizza" (escrito con acento agudo `´` en lugar de apóstrofo `'`).
- Ubicación: "Medellin" (solo ciudad, sin dirección).
- Horarios: lunes a jueves 2:00 pm - 10:30 pm; viernes a domingo "12m" - 10:30 pm (se interpreta 12 del mediodía).
- Teléfono / WhatsApp: 3113706576 (sin indicativo de país; se asume Colombia, +57).
- Redes sociales: "[por definir]".
- Menú de ejemplo ("reemplazar por el real"): Carnes, Champiñon con pollo, Mexicana; cada una "[10.500]".

### Tono y estilo
- Comercial, juvenil, llamativo; inspirado en comida rápida y cadenas de pizza; energía, apetito, facilidad de compra; estética audaz y ligeramente retro/rústica.
- Paleta: rojo, amarillo, crema, marrón oscuro, negro.
- Tipografías grandes, gruesas y expresivas; contrastes fuertes; formas dinámicas; elementos tipo stickers, etiquetas, sellos y promociones.
- No hay logo.

### Fuera de alcance
- Pagos en línea, carrito de pedidos, panel de administración.

### Huecos y ambigüedades
- Sin dirección exacta: no se puede hacer un mapa preciso ni un enlace de "cómo llegar".
- Menú: solo 3 pizzas. No hay entradas, bebidas ni postres, aunque el brief pide esas cuatro categorías.
- Los productos no tienen descripción ni imagen (el modelo previsto tiene ambos campos).
- No se indica cuáles pizzas son destacadas.
- Precio "10.500": moneda no indicada (se asume COP, diez mil quinientos, con punto como separador de miles). No se indica tamaño ni si es porción o pizza completa.
- "Sobre nosotros": no hay texto.
- Redes sociales sin definir: el pie "con redes" no tiene contenido.
- Formulario "contacto / reservas": no se define si una reserva necesita campos propios (fecha, hora, número de personas) o si es un mensaje libre.
- El hero pide "pedir por WhatsApp / reservar": no se define el texto prellenado del mensaje de WhatsApp ni a dónde lleva "reservar" (se supone que al formulario).
- No hay eslogan ni textos comerciales.
- Sin panel de administración, los mensajes guardados solo se podrán consultar directamente en la base de datos.

---

## 4. Restricciones y convenciones aplicables

### De `CLAUDE.md`
- Stack: Razor Pages en .NET LTS, EF Core + SQL Server code-first con migraciones, CSS propio en `wwwroot/css`, JS mínimo, xUnit. Despliegue previsto: Azure App Service + Azure SQL.
- Estructura: `src/Pizzeria/` (`Pages/`, `Models/`, `Data/`, `wwwroot/`), `tests/Pizzeria.Tests/`, `docs/specs/<feature>/`.
- Código y nombres en inglés; textos visibles en español.
- Un `AppDbContext` en `Data/`; entidades en `Models/`.
- Validación en servidor con DataAnnotations; formularios con antiforgery.
- Acceso a datos async/await con inyección de dependencias; solo LINQ/EF, nada de SQL crudo o concatenado.
- Landing responsive (móvil primero), accesible (alt, contraste, etiquetas) y rápida.
- Modelo inicial: `MenuItem` (nombre, descripción, precio, categoría, imagen, destacado) y `ContactMessage` (nombre, teléfono o correo, mensaje, fecha); ajustable en la propuesta.
- Reglas duras: sin secretos en código ni repo (user-secrets en local, App Settings en Azure); no ejecutar migraciones contra producción ni despliegues sin confirmación explícita; no tocar nada fuera del directorio; si una corrección falla dos veces, detenerse y explicar.
- Flujo: aprobación del usuario antes de implementar; cambios y commits pequeños (menos de 400 líneas por PR).

### Del skill `aspnet-efcore-sqlserver`
- `DbSet<T>` por entidad; Fluent API en `OnModelCreating` (longitudes máximas, índices, relaciones).
- Precios `decimal` con precisión explícita (`HasPrecision(10, 2)`).
- `AsNoTracking()` en lecturas; todo async.
- Datos de ejemplo con `HasData` o seeder solo en desarrollo; no sembrar en producción sin avisar.
- Migraciones con nombres descriptivos; revisar el archivo generado antes de aplicarlo.
- `ConnectionStrings:Default` (Azure: `ConnectionStrings__Default`); `EnableRetryOnFailure()`.
- Formularios: `[BindProperty]` sobre un modelo con DataAnnotations, `ModelState.IsValid`, no exponer entidades completas a la vista, post-redirect-get con confirmación, lógica en servicios pequeños si hay más que una consulta simple.

### Del skill `seguridad-web-basica` (checklist a cumplir al terminar)
1. Sin secretos en código, `appsettings*.json` ni historial.
2. Antiforgery, validación en servidor, longitudes máximas, honeypot o límite de peticiones.
3. Rate limiting (`AddRateLimiter`) en el formulario de contacto.
4. Sin `Html.Raw` con datos del usuario.
5. Solo LINQ/EF; guardar los mínimos datos personales.
6. HTTPS forzado, HSTS en producción, `X-Content-Type-Options`, `Referrer-Policy`, CSP razonable.
7. Errores genéricos en producción.
8. No registrar datos personales ni secretos en logs.
9. Ejecutar `dotnet list package --vulnerable`.
10. Usuario de base de datos de producción sin rol de administrador.

---

## 5. Riesgos y preguntas abiertas

### Riesgos
- **Versión de .NET sin verificar.** El proyecto apunta a `net10.0` (LTS) y ya compiló, pero no se ejecutó `dotnet --version`. No hay `global.json` que fije el SDK.
- **`dotnet-ef` y SQL Server local sin verificar.** Si falta la herramienta o la instancia, la fase de migraciones se bloquea. La versión de `dotnet-ef` debe ser compatible con EF Core 10.0.12.
- **Cadena de conexión ausente.** Si `ConnectionStrings:Default` no está en user-secrets, la app debe fallar con un mensaje claro y no con una excepción confusa. Además, la landing depende de la base de datos para mostrar el menú: si la base no responde, toda la página falla salvo que se maneje el error.
- **Cambio sin commitear en `Pizzeria.csproj`.** Conviene commitearlo aparte antes de empezar para que no se mezcle con la funcionalidad.
- **Bootstrap/jQuery frente a "CSS propio".** Retirarlos implica reescribir layout y perder la validación de cliente no intrusiva; conservarlos contradice la convención y pesa más.
- **CSP frente a recursos externos.** Un mapa incrustado (iframe de Google Maps) o fuentes web externas obligan a abrir la CSP. El `<script type="importmap">` del layout y cualquier script o estilo en línea también chocan con una CSP estricta.
- **Formato de precios.** "10.500" debe guardarse como `10500` y mostrarse con formato colombiano; depende de fijar la cultura (el servidor de Azure no usará `es-CO` por defecto).
- **Fechas.** La fecha de `ContactMessage` debería guardarse en UTC; Azure corre en UTC y Medellín es UTC-5.
- **Contenido insuficiente.** Con 3 pizzas sin descripción ni foto y tres categorías vacías, la landing quedará con secciones vacías o con contenido inventado. Sembrar contenido inventado debe ser una decisión explícita del usuario.
- **Imágenes.** No hay fotos ni logo; hay que definir marcadores (y su licencia) y cuidar peso y `alt`.
- **Datos personales.** El formulario guarda nombre y teléfono/correo. En Colombia aplica la Ley 1581 de 2012 (habeas data): conviene un aviso de tratamiento de datos, lo que afecta a la decisión sobre la página `Privacy`.
- **Tamaño del cambio.** La funcionalidad completa supera con holgura las 400 líneas; habrá que dividirla en varios bloques/PR.
- **Pruebas con EF.** El proveedor de pruebas no se comporta igual que SQL Server (precisión de decimales, restricciones); hay que elegirlo con cuidado.

### Preguntas para el usuario
1. ¿Entran "Sobre nosotros" y el pie con redes en esta funcionalidad? Si es así, ¿qué texto y qué redes?
2. ¿Cuál es la dirección exacta? ¿Mapa incrustado, enlace a Google Maps o solo texto?
3. ¿Hay menú real (con descripciones, entradas, bebidas, postres)? Si no, ¿se pueden inventar productos de ejemplo o se ocultan las categorías vacías?
4. ¿Qué pizzas son destacadas?
5. ¿"10.500" son pesos colombianos? ¿Hay tamaños con precios distintos?
6. ¿El formulario es solo de contacto o debe capturar datos de reserva (fecha, hora, personas)? ¿Teléfono y correo son ambos opcionales con al menos uno obligatorio?
7. ¿El WhatsApp es +57 311 370 6576? ¿Algún mensaje prellenado para el botón de pedido?
8. ¿Se retiran Bootstrap y jQuery de la plantilla?
9. ¿Hay logo o fotos? ¿Se permite cargar tipografías externas (por ejemplo Google Fonts) o deben alojarse en el proyecto?
10. ¿El nombre se escribe "Harry's Pizza" con apóstrofo normal?
11. ¿Qué SQL Server se usa en local (LocalDB, Express, contenedor) y ya está guardada la cadena en user-secrets? ¿Está instalada `dotnet-ef`?
12. ¿Se elimina la página `Privacy` o se convierte en aviso de tratamiento de datos?
