# 04 - Resumen: `landing-page`

Fase 7 del flujo SDD. Estado al 2026-10-03: implementado y verificado en local; sin commits todavía.

## Qué se hizo

Landing de una sola página para Harry's Pizza, según `docs/brief.md` y la propuesta aprobada (`02-propuesta.md`):

- **Secciones:** cabecera con anclas, hero con "Pedir por WhatsApp" y "Reservar", pizzas destacadas, menú por categorías, "Sobre nosotros", horarios y ubicación, formulario de contacto y pie. Barra fija de WhatsApp en móvil.
- **Datos:** `MenuItem` y `ContactMessage` en SQL Server con EF Core (code-first). Migración `InitialCreate`. El menú de ejemplo se siembra con `DbSeeder` solo en Development.
- **Formulario:** guarda en `ContactMessages` con validación en servidor, antiforgery, Post-Redirect-Get, honeypot y límite de 5 envíos cada 10 minutos por IP (429).
- **Seguridad:** cabeceras y CSP estricta (sin estilos ni scripts en línea, sin recursos de terceros), sin secretos en el repo, sin datos personales en logs.
- **Diseño:** CSS propio (se retiraron Bootstrap y jQuery), paleta rojo/amarillo/crema/marrón/negro, fuentes Bowlby One y Archivo alojadas en `wwwroot/fonts/` (109 KB), ilustración SVG propia como marcador de imagen. Sin JavaScript.
- **Aviso de privacidad** en `/privacidad`, enlazado desde el formulario y el pie.
- **Si la base no responde:** la página devuelve 200 y sustituye destacadas y menú por un aviso; el formulario muestra un error general y conserva lo escrito. Sin `ConnectionStrings:Default`, la app falla al arrancar con un mensaje que indica el comando de user-secrets.

## Archivos

Bajo `src/Pizzeria/`:

| Carpeta | Contenido |
|---|---|
| `Models/` | `MenuItem`, `MenuCategory`, `ContactMessage`, `ContactInput` (entrada del formulario), `BusinessInfo` (opciones del negocio) |
| `Data/` | `AppDbContext`, `DbSeeder` |
| `Services/` | `MenuService` (y `MenuCategoryGroup`), `ContactService`, `PriceFormatter`, `MenuCategoryNames` |
| `Migrations/` | `InitialCreate` y snapshot |
| `Pages/` | `Index` (GET y POST), `Privacy` (ruta `/privacidad`), `Error` |
| `Pages/Shared/` | `_Layout`, `_Hero`, `_Featured`, `_Menu`, `_About`, `_HoursLocation`, `_ContactForm` |
| `wwwroot/` | `css/site.css`, `fonts/` (2 woff2 y sus licencias OFL), `img/pizza-placeholder.svg`, `favicon.svg` |
| raíz | `Program.cs` (DbContext, servicios, rate limiter, cabeceras), `appsettings.json` (sección `Business`) |

Eliminado de la plantilla: `wwwroot/lib/`, `wwwroot/js/site.js`, `wwwroot/favicon.ico`, `_ValidationScriptsPartial.cshtml`, `_Layout.cshtml.css`.

`tests/Pizzeria.Tests/`: proyecto xUnit con 292 pruebas (unitarias con SQLite en memoria e integración con `WebApplicationFactory`). Añadido a `Pizzeria.slnx`.

## Cómo probarlo

1. Cadena de conexión en user-secrets (ya configurada en esta máquina, apunta a LocalDB):
   `dotnet user-secrets set "ConnectionStrings:Default" "<cadena>" --project src/Pizzeria`
2. `dotnet ef database update --project src/Pizzeria` (ya aplicada en LocalDB, base `Pizzeria`).
3. `dotnet test` (292 pruebas).
4. `dotnet run --project src/Pizzeria --launch-profile http` y abrir `http://localhost:5227`. En Development el arranque siembra las 3 pizzas si la tabla está vacía.
5. Enviar el formulario: debe volver a `#contacto` con "Mensaje enviado" y crear una fila en `ContactMessages`.

## Verificación realizada

- Build sin advertencias y 292 pruebas superadas. Sin paquetes vulnerables.
- Revisión de seguridad: `05-verificacion.md` (checklist `seguridad-web-basica`, puntos 1 a 9 cumplidos; el 10 depende del despliegue).
- En navegador (Edge, emulando 320, 360, 768 y 1280 px): sin scroll horizontal; diseño conforme a la sección 7 de la propuesta.
- Formulario de extremo a extremo contra LocalDB: envío válido guardado, envío sin teléfono ni correo rechazado con su mensaje, envío sin token antiforgery respondido con 400.

No verificado: navegación con teclado y foco visible, `prefers-reduced-motion`, lector de pantalla, errores de CSP en la consola del navegador, y el comportamiento con SQL Server detenido en local (cubierto solo por pruebas automáticas con un servicio que falla).

## Desviaciones respecto a la propuesta

- Tamaño mínimo del nombre en el hero: `2.75rem` en lugar de `3.5rem`, para que no se parta a 320 px.
- Botón pequeño, sello "Destacada" y aviso "Mensaje enviado" sin la sombra de 6 px.
- Color adicional `--brown-soft: #6B4A3A` para descripciones y ayudas (7,03:1 sobre crema).
- Con el menú vacío se muestra el mismo aviso que con la base caída.
- La validación del teléfono exige al menos 7 dígitos (la expresión original aceptaba un dígito seguido de espacios).
- Añadidos: enlace "Saltar al contenido", meta description, claves `Business:City`, `Business:WhatsAppMessage` y `Business:LegalName`.

## Pendiente antes de publicar

- Rellenar `Business:LegalName` y revisar el texto del aviso de privacidad (orientativo, no es asesoría legal).
- Sustituir el contenido de ejemplo: eslogan del hero, "Sobre nosotros", descripciones de las pizzas, menú real, dirección y redes.
- Fotos reales de las pizzas (guía en la sección 7.4 de la propuesta).
- Para el despliegue (otra funcionalidad): `ForwardedHeaders` para que el límite de envíos vea la IP real, cookie antiforgery con `SecurePolicy = Always`, `AllowedHosts`, y usuario de base de datos no administrador.
- En LocalDB quedó un mensaje de prueba ("Prueba SDD") en `ContactMessages`.
