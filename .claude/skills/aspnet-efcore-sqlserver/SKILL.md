---
name: aspnet-efcore-sqlserver
description: Convenciones para ASP.NET Core Razor Pages con Entity Framework Core y SQL Server (DbContext, modelos, migraciones, consultas, configuración). Úsalo al crear o modificar entidades, acceso a datos, páginas o migraciones.
---

# ASP.NET Core + EF Core + SQL Server

## Datos
- Un `AppDbContext` con `DbSet<T>` por entidad. Configura detalles en `OnModelCreating` con Fluent API (longitudes máximas, índices, relaciones).
- Los precios usan `decimal` con precisión explícita (por ejemplo `HasPrecision(10, 2)`).
- Consultas de solo lectura: `AsNoTracking()`. Todo el acceso a datos es async (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).
- Datos iniciales (menú de ejemplo): `HasData` en el modelo o un seeder que se ejecute en desarrollo. No cargues datos de ejemplo en producción sin avisar.
- Migraciones con nombres descriptivos (`AddMenuItems`, `AddContactMessages`). Revisa el archivo generado antes de aplicarlo.

## Configuración
- La cadena de conexión se lee de `ConnectionStrings:Default`. En local va en user-secrets; en Azure, en App Settings como `ConnectionStrings__Default`.
- Al registrar `UseSqlServer`, activa `EnableRetryOnFailure()` (útil con Azure SQL).

## Páginas
- Cada página con formulario: `PageModel` con `[BindProperty]` sobre un modelo con DataAnnotations. Comprueba `ModelState.IsValid` antes de guardar.
- Muestra a la vista solo lo necesario: no expongas entidades completas si no hace falta.
- Patrón post-redirect-get tras guardar un formulario, con mensaje de confirmación.
- Mantén la lógica de negocio fuera de las páginas (servicios pequeños inyectados) cuando haya más que una consulta simple.
