# Pizzería — Landing page

## Qué es
Landing page para una pizzería (proyecto de aprendizaje). Muestra el menú (con pizzas destacadas), horarios, ubicación y un formulario de contacto/reservas que guarda los mensajes en SQL Server. El contenido real del negocio está en `docs/brief.md`.

## Stack
- ASP.NET Core Razor Pages en .NET LTS (verificar con `dotnet --version`; usar la LTS instalada)
- Entity Framework Core con SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`), code-first con migraciones
- CSS propio en `wwwroot/css`; JavaScript mínimo, sin frameworks pesados
- Pruebas: xUnit
- Despliegue previsto: Azure App Service + Azure SQL Database

## Modelo de datos
- `MenuItem`: nombre, descripción, precio, categoría (enum `MenuCategory` guardado como texto), imagen, destacado
- `ContactMessage`: nombre, teléfono y correo (columnas separadas, al menos uno), mensaje, fecha en UTC
- El formulario enlaza `ContactInput`, nunca la entidad
- Menú de ejemplo: `Data/DbSeeder.cs`, solo en Development (no usar `HasData`)

## Estructura
- `src/Pizzeria/` — app web (Pages/, Models/, Data/, Services/, Migrations/, wwwroot/)
- `src/Pizzeria/Pages/Shared/` — un parcial por sección de la landing
- `src/Pizzeria/appsettings.json`, sección `Business` — datos del negocio (no secretos)
- `src/Pizzeria/wwwroot/` — `css/site.css`, `fonts/` (alojadas, sin recursos de terceros), `img/`
- `tests/Pizzeria.Tests/` — pruebas
- `docs/brief.md` — información del negocio y requisitos de la landing
- `docs/specs/<feature>/` — artefactos del flujo SDD

## Comandos
- Compilar: `dotnet build`
- Probar: `dotnet test`
- Ejecutar: `dotnet run --project src/Pizzeria --launch-profile http` (http://localhost:5227)
- Nueva migración: `dotnet ef migrations add <Nombre> --project src/Pizzeria`
- Aplicar migraciones en local: `dotnet ef database update --project src/Pizzeria`

## Convenciones
- Código y nombres en inglés; textos visibles para el usuario en español
- Un `AppDbContext` en `Data/`; entidades en `Models/`
- Validación en servidor con DataAnnotations; formularios con antiforgery
- CSP estricta: sin estilos ni scripts en línea (ni atributos `style`)
- Pruebas con SQLite en memoria y `PizzeriaWebFactory`; nunca contra SQL Server
- Acceso a datos siempre async/await; usar inyección de dependencias
- Nada de SQL concatenado ni consultas crudas; usar LINQ/EF
- Landing responsive (móvil primero), accesible (alt en imágenes, contraste, etiquetas en formularios) y rápida

## Reglas duras
- Nunca poner cadenas de conexión ni secretos en el código o el repo. Local: user-secrets. Azure: App Settings / variables de entorno
- No ejecutar migraciones contra producción ni comandos de despliegue sin mi confirmación explícita
- No tocar archivos fuera de este directorio
- Si una corrección falla dos veces seguidas, detente y explícame qué pasa antes de seguir

## Flujo de trabajo
- Para funcionalidades no triviales usar el skill `sdd` (artefactos en `docs/specs/<feature>/`)
- Pedir mi aprobación antes de pasar de la propuesta a la implementación
- Cambios y commits pequeños (idealmente menos de 400 líneas por PR)
- Cambios triviales (texto, estilos): hacerlos directo, sin flujo completo
- Skills del proyecto a consultar: `aspnet-efcore-sqlserver` (datos y estructura), `seguridad-web-basica` (revisión de seguridad)

- Para cualquier trabajo de interfaz (HTML, CSS, layout) usar el skill frontend-design.
