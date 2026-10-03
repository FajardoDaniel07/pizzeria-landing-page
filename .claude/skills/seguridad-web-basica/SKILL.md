---
name: seguridad-web-basica
description: Checklist de seguridad básica para una aplicación web ASP.NET Core con formulario público y base de datos. Úsalo al revisar o terminar una funcionalidad, antes de entregar o desplegar.
---

# Checklist de seguridad básica

Revisa y reporta cada punto como cumple / no cumple:

1. **Secretos:** ninguna cadena de conexión, clave o contraseña en código, `appsettings*.json` versionados ni en el historial de git.
2. **Formularios:** antiforgery activo, validación en servidor, longitudes máximas, y un campo trampa (honeypot) o límite de peticiones contra spam en el formulario público.
3. **Límite de peticiones:** el formulario de contacto tiene rate limiting (`AddRateLimiter`).
4. **Salida segura:** no usar `Html.Raw` con datos del usuario; Razor ya codifica por defecto.
5. **Datos:** solo LINQ/EF; nada de SQL concatenado. Evita guardar más datos personales de los necesarios.
6. **Transporte y cabeceras:** HTTPS forzado, HSTS en producción y cabeceras básicas (`X-Content-Type-Options`, `Referrer-Policy`, una CSP razonable).
7. **Errores:** en producción, páginas de error genéricas, sin trazas ni detalles de la base de datos.
8. **Logs:** no registrar datos personales ni secretos.
9. **Dependencias:** ejecutar `dotnet list package --vulnerable` y reportar resultados.
10. **Base de datos:** el usuario de la app en producción no debe ser administrador del servidor.
