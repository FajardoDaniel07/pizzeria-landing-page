---
name: sdd
description: Flujo de desarrollo guiado por especificaciones (SDD) para una funcionalidad nueva o un cambio no trivial. Orquesta subagentes por fase (exploración, propuesta, requisitos, implementación, pruebas, documentación, entrega) y se detiene para pedir aprobación al usuario en los puntos clave.
---

# Flujo SDD

Recibes una funcionalidad a desarrollar. Tú eres el orquestador: delegas cada fase a un subagente y avanzas solo cuando la fase anterior terminó. Los subagentes no se comunican entre sí; la información pasa por archivos en `docs/specs/<feature>/` (usa un nombre corto en kebab-case para `<feature>`).

## Fases

1. **Exploración** — subagente `explorer`. Resultado: `01-exploracion.md`.
2. **Propuesta** — subagente `proposer`. Resultado: `02-propuesta.md`.
   **DETENTE**: muéstrame un resumen corto y espera mi aprobación o ajustes.
3. **Requisitos** — subagente `spec-writer`. Resultado: `03-requisitos.md` (funcionales y no funcionales, con criterios de aceptación).
   Si hay dudas abiertas, pregúntamelas antes de seguir.
4. **Pruebas primero (lógica de negocio)** — subagente `tester` escribe las pruebas según los requisitos. Si es solo UI/estilos, omite esta fase.
5. **Implementación** — subagente `implementer`, por bloques pequeños, hasta que build y pruebas pasen.
6. **Verificación** — subagente `tester` corre `dotnet build` y `dotnet test` y hace una revisión básica de seguridad (secretos, antiforgery, validación, SQL).
7. **Documentación** — escribe `04-resumen.md`: qué se hizo, qué archivos cambiaron, cómo probarlo. Actualiza `CLAUDE.md` si cambió la estructura o los comandos.
8. **Entrega** — propón commits/PR pequeños. El subagente `deployer` solo se usa si yo lo pido.

## Reglas
- Cada subagente responde con un resumen de máximo 15 líneas; los detalles van en los archivos.
- Solo un subagente edita código a la vez.
- Si una fase falla dos veces, detente y explícame el problema.
- No pases a la implementación sin mi aprobación de la propuesta.
