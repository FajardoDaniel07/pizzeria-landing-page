# 03 - Requisitos: `landing-page`

Fase 3 del flujo SDD. Deriva de `02-propuesta.md` (aprobada el 2026-10-03, incluida la sección 7), `01-exploracion.md`, `CLAUDE.md` y `docs/brief.md`. No se modificó código.

Entorno verificado: SDK .NET 10.0.401, `dotnet-ef` 10.0.12, `ConnectionStrings:Default` presente en user-secrets, LocalDB y SQLEXPRESS disponibles.

## Cómo leer este documento

- **RF-nn**: requisito funcional. **RNF-nn**: requisito no funcional.
- Cada requisito indica el **bloque** del plan de trabajo (sección 4 de la propuesta) y cómo se **verifica**:
  - **U**: prueba unitaria (xUnit, sin servidor web).
  - **I**: prueba de integración (`WebApplicationFactory<Program>` + SQLite en memoria).
  - **M**: revisión manual (navegador, herramienta de contraste, lectura de código o de archivos).
- Los textos entre comillas son literales: deben aparecer tal cual en la página.
- Los textos de los mensajes de validación (RF-18) y los identificadores de ancla distintos de `#contacto` (RF-02) no estaban en la propuesta; se fijan aquí para poder probarlos. Son detalle de implementación, no alcance nuevo. Los demás supuestos adoptados están en la sección 5.

---

## 1. Requisitos funcionales

### 1.1 Layout y navegación

**RF-01 - Layout base en español** (bloque 4b; I, M)
- [ ] `GET /` responde 200 y el HTML contiene `<html lang="es">`.
- [ ] El `<title>` y la cabecera contienen el nombre del negocio leído de `Business:Name` ("Harry's Pizza", con apóstrofo recto).
- [ ] El HTML no contiene las cadenas `bootstrap`, `jquery`, `importmap` ni `Pizzeria.styles.css`.
- [ ] El HTML no contiene elementos `<style>`, atributos `style="..."` ni `<script>` en línea.
- [ ] El favicon enlazado es `favicon.svg`.
- [ ] No queda texto visible en inglés de la plantilla ("Welcome", "Home", "Privacy").

**RF-02 - Cabecera con anclas** (bloque 4b; I, M)
- [ ] La cabecera muestra el nombre del negocio y enlaces de ancla "Menú" (`#menu`), "Horarios" (`#horarios`) y "Contacto" (`#contacto`), más un botón de WhatsApp con la URL de RF-06.
- [ ] Cada ancla lleva a una sección existente con ese `id` en la misma página.
- [ ] A 360 px de ancho la cabecera muestra solo el nombre y el enlace "Menú"; a 1280 px muestra los tres enlaces y el botón de WhatsApp.

**RF-03 - Pie de página** (bloque 4b; I, M)
- [ ] El pie muestra el nombre del negocio, el teléfono, un enlace de WhatsApp y un enlace a `/privacidad`.
- [ ] Dado `Business:Social` vacío, cuando se carga `/`, entonces el pie no contiene iconos ni enlaces de redes sociales ni un título de redes vacío.

**RF-04 - Retirada de la plantilla** (bloque 4a; M)
- [ ] No existen `wwwroot/lib/`, `Pages/Shared/_ValidationScriptsPartial.cshtml` ni `Pages/Shared/_Layout.cshtml.css`.
- [ ] `wwwroot/js/site.js` no existe (salvo que la implementación necesite JavaScript; en ese caso se justifica en el PR).
- [ ] `dotnet build` y `dotnet test` siguen en verde tras los borrados.

**RF-05 - Página de error en español** (bloque 4b; M)
- [ ] `Error.cshtml` muestra un texto genérico en español, sin el párrafo "Development Mode" y sin trazas ni detalles técnicos.

### 1.2 Hero

**RF-06 - Contenido y llamadas a la acción** (bloque 5; I, M)
- [ ] El hero muestra el nombre del negocio, un eslogan de una línea y dos botones: "Pedir por WhatsApp" y "Reservar".
- [ ] El `href` de "Pedir por WhatsApp" empieza por `https://wa.me/573113706576?text=` y el parámetro `text`, una vez decodificado, es exactamente `Hola, quiero hacer un pedido`. El valor está codificado en URL (sin espacios ni comas literales).
- [ ] El `href` de "Reservar" es `#contacto`.
- [ ] El eslogan está marcado en `_Hero.cshtml` con el comentario Razor `@* TEXTO DE EJEMPLO *@`.
- [ ] La ilustración de la pizza del hero es decorativa y lleva `alt=""`.

**RF-07 - Sticker "Desde $X"** (bloque 6; U, I)
- [ ] Dado el menú sembrado, cuando se carga `/`, entonces el hero muestra el texto "Desde $10.500".
- [ ] El valor es el precio más bajo de todos los `MenuItem`, calculado en `MenuService` (prueba unitaria con precios distintos, por ejemplo 12000 y 9500 da 9500).
- [ ] Dado el menú no disponible (RF-29) o sin productos, entonces el sticker no se pinta (el HTML no contiene "Desde").

**RF-08 - Barra fija de WhatsApp en móvil** (bloque 5; I, M)
- [ ] El HTML contiene una barra con un enlace "Pedir por WhatsApp" con la misma URL de RF-06.
- [ ] A 360 px de ancho la barra queda fija en el borde inferior durante todo el scroll; a 1280 px no se muestra.
- [ ] Está hecha solo con CSS (sin JavaScript).
- [ ] Con la barra visible, el último contenido de la página (pie) se puede leer completo: el contenido deja margen inferior y la barra no tapa nada.

### 1.3 Pizzas destacadas

**RF-09 - Sección de destacadas** (bloque 6; I, M)
- [ ] Dado el menú sembrado, la sección muestra exactamente dos tarjetas: "Carnes" y "Mexicana" (los `MenuItem` con `IsFeatured = true`). "Champiñón con pollo" no aparece en esta sección.
- [ ] Cada tarjeta muestra imagen, nombre, descripción y precio con el formato de RF-12.
- [ ] Dado `ImagePath` nulo, la imagen es `/img/pizza-placeholder.svg` con `alt` igual al nombre de la pizza.
- [ ] El título de la sección es "Pizzas destacadas".

### 1.4 Menú

**RF-10 - Menú por categorías** (bloque 6; U, I)
- [ ] La sección (`id="menu"`) agrupa los productos por categoría y, dentro de cada una, los ordena por nombre. Con el seed: "Carnes", "Champiñón con pollo", "Mexicana".
- [ ] Nombres visibles de las categorías: `Pizza` = "Pizzas", `Starter` = "Entradas", `Drink` = "Bebidas", `Dessert` = "Postres". Orden de categorías: el del enum.
- [ ] Las categorías sin productos no se muestran: con el seed, el HTML contiene "Pizzas" como título de categoría y no contiene "Entradas", "Bebidas" ni "Postres".
- [ ] Cada fila muestra nombre, precio y, si existe, descripción.

**RF-11 - Sello "Destacada" en el menú** (bloque 6; I)
- [ ] Las filas de "Carnes" y "Mexicana" llevan el texto "Destacada"; la de "Champiñón con pollo" no.

**RF-12 - Formato de precios** (bloque 3; U, I)
- [ ] `PriceFormatter.ToCop(10500m)` devuelve exactamente `$10.500`.
- [ ] `ToCop(9500m)` devuelve `$9.500`; `ToCop(0m)` devuelve `$0`; `ToCop(1250000m)` devuelve `$1.250.000`. Sin decimales y sin espacio entre `$` y la cifra.
- [ ] El resultado es el mismo con `CultureInfo.CurrentCulture` puesta en `en-US` y en `es-CO` (no depende de la cultura del servidor).
- [ ] `GET /` con el seed contiene `$10.500`.

### 1.5 Sobre nosotros

**RF-13 - Sección "Sobre nosotros"** (bloque 5; I, M)
- [ ] La página contiene una sección con el título "Sobre nosotros" y dos frases.
- [ ] Las frases no incluyen datos inventados: ni años, ni premios, ni historia del negocio.
- [ ] `_About.cshtml` lleva el comentario Razor `@* TEXTO DE EJEMPLO *@`.
- [ ] La sección no tiene imagen.

### 1.6 Horarios y ubicación

**RF-14 - Horarios** (bloque 5; I, M)
- [ ] La sección (`id="horarios"`) muestra los horarios en una tabla de días y horas con dos filas: "Lunes a jueves" - "2:00 p. m. a 10:30 p. m." y "Viernes a domingo" - "12:00 m. a 10:30 p. m.".
- [ ] Los horarios se leen de la sección `Business` de `appsettings.json`, no están escritos en la vista.

**RF-15 - Ubicación** (bloque 5; I)
- [ ] Se muestra "Medellín, Antioquia".
- [ ] Dado `Business:Address` vacío, entonces no hay línea de dirección y el HTML no contiene "Cómo llegar".
- [ ] Dado `Business:Address` con valor, entonces se muestra la dirección y un enlace "Cómo llegar" a Google Maps (enlace normal, sin `iframe`).
- [ ] En ningún caso la página contiene un `iframe`.

### 1.7 Formulario de contacto

**RF-16 - Campos del formulario** (bloque 7; I, M)
- [ ] El formulario está en `/`, dentro de la sección `id="contacto"`, con `method="post"` y una acción que termina en `#contacto`.
- [ ] Campos visibles, cada uno con su `<label>` asociado por `for`/`id`: "Nombre" (`type="text"`, `required`, `maxlength="80"`), "Teléfono" (`type="tel"`, `maxlength="20"`), "Correo" (`type="email"`, `maxlength="254"`), "Mensaje" (`textarea`, `required`, `maxlength="1000"`).
- [ ] Bajo el campo "Mensaje" aparece el texto de ayuda "Para reservar, indica fecha, hora y número de personas".
- [ ] El botón de envío dice "Enviar mensaje".
- [ ] No hay campos de fecha, hora ni número de personas.
- [ ] El formulario incluye un enlace a `/privacidad`.
- [ ] La vista enlaza `ContactInput`, nunca la entidad `ContactMessage`.

**RF-17 - Envío correcto** (bloque 7; I)
- [ ] Dado un POST con token antiforgery, `Name = "Ana"`, `Phone = "311 370 6576"`, `Email` vacío y `Message = "Quiero reservar"`, cuando se envía, entonces se guarda exactamente una fila en `ContactMessages` y la respuesta es 302 con `Location` terminado en `/#contacto`.
- [ ] Lo mismo con `Phone` vacío y `Email = "ana@example.com"`, y con ambos rellenos.
- [ ] Al seguir la redirección, la página muestra "Mensaje enviado" y los campos del formulario están vacíos.
- [ ] Una segunda carga de `/` ya no muestra "Mensaje enviado" (el aviso viaja en `TempData["ContactOk"]` y se consume una vez).

**RF-18 - Envío con errores de validación** (bloque 3 y 7; U, I)

Dado un POST con datos inválidos, entonces la respuesta es 200, no se guarda ninguna fila, los valores escritos se conservan en los campos y junto a cada campo aparece su mensaje precedido de "Error:".

| Caso | Mensaje exacto |
|---|---|
| `Name` vacío o solo espacios | "Escribe tu nombre." |
| `Name` con 1 carácter o más de 80 | "El nombre debe tener entre 2 y 80 caracteres." |
| `Phone` que no cumple `^\+?(?: *[0-9]){7,} *$` (al menos 7 dígitos; corregido en la verificación, defecto D-3) o con más de 20 caracteres (por ejemplo `abc`, `12345`, `311-370-6576`) | "Escribe un teléfono válido: de 7 a 20 caracteres, solo números y espacios." |
| `Email` con formato inválido (por ejemplo `ana@`) | "Escribe un correo válido." |
| `Email` con más de 254 caracteres | "El correo no puede superar los 254 caracteres." |
| `Message` vacío o solo espacios | "Escribe tu mensaje." |
| `Message` con menos de 5 o más de 1000 caracteres | "El mensaje debe tener entre 5 y 1000 caracteres." |
| `Phone` y `Email` vacíos los dos | "Indica al menos un teléfono o un correo para poder responderte." |

- [ ] Valores límite válidos: `Name` de 2 y de 80 caracteres; `Message` de 5 y de 1000; `Phone` `3113706576` y `+57 311 370 6576`.
- [ ] La regla "al menos teléfono o correo" está implementada con `IValidatableObject` en `ContactInput`.
- [ ] Las pruebas unitarias de `ContactInput` cubren cada fila de la tabla y los valores límite; la integración cubre al menos un caso inválido de extremo a extremo.

### 1.8 Privacidad

**RF-19 - Aviso de tratamiento de datos** (bloque 8; I, M)
- [ ] `GET /privacidad` responde 200 con `lang="es"` y el layout común.
- [ ] El texto es corto y en español, e indica: qué datos se recogen (nombre, teléfono, correo y mensaje), para qué (responder la consulta o reserva), el responsable (el negocio, desde `Business:Name`) y la referencia a la Ley 1581 de 2012.
- [ ] El texto declara que no se guarda la dirección IP ni el agente de usuario junto al mensaje.
- [ ] La página está enlazada desde el formulario (RF-16) y desde el pie (RF-03).
- [ ] El archivo sigue siendo `Pages/Privacy.cshtml` (nombre en inglés) con la ruta `/privacidad`.

### 1.9 Datos y persistencia

**RF-20 - Entidad `MenuItem`** (bloque 2; I con SQLite, M sobre la migración)
- [ ] Propiedades y columnas: `Id` (`int`, PK identity), `Name` (obligatorio, `nvarchar(80)`), `Description` (opcional, `nvarchar(300)`), `Price` (`decimal(10,2)` con `HasPrecision(10, 2)`), `Category` (enum `MenuCategory { Pizza, Starter, Drink, Dessert }` guardado como texto, `nvarchar(20)`, con `HasConversion<string>()`), `ImagePath` (opcional, `nvarchar(200)`), `IsFeatured` (`bit`, por defecto `false`).
- [ ] Existe un índice sobre `Category`.
- [ ] `AppDbContextTests` comprueba, sobre los metadatos del modelo, las longitudes máximas (80, 300, 20, 200) y la precisión (10, 2).

**RF-21 - Entidad `ContactMessage`** (bloque 2; I con SQLite, M sobre la migración)
- [ ] Propiedades y columnas: `Id` (`int`, PK identity), `Name` (obligatorio, `nvarchar(80)`), `Phone` (opcional, `nvarchar(20)`), `Email` (opcional, `nvarchar(254)`), `Message` (obligatorio, `nvarchar(1000)`), `CreatedAtUtc` (`datetime2(0)`).
- [ ] La entidad no tiene propiedades para IP ni agente de usuario.
- [ ] `AppDbContextTests` comprueba las longitudes máximas (80, 20, 254, 1000).

**RF-22 - `AppDbContext` y migración `InitialCreate`** (bloque 2; I, M)
- [ ] Hay un único `AppDbContext` en `Data/` con `DbSet<MenuItem> MenuItems` y `DbSet<ContactMessage> ContactMessages`, configurado con Fluent API.
- [ ] El modelo se crea sin errores en SQLite en memoria (`EnsureCreated`).
- [ ] Existe una sola migración, `InitialCreate`, que crea las dos tablas y el índice; el archivo generado se revisa antes de aplicarlo y no contiene `InsertData` (no se usa `HasData`).
- [ ] La migración se aplica solo en local y a mano con `dotnet ef database update --project src/Pizzeria`.
- [ ] El código no llama a `Database.Migrate()` ni a `MigrateAsync()` al arrancar.

**RF-23 - Datos de ejemplo (`DbSeeder`)** (bloque 3; U/I con SQLite, M)
- [ ] Dado el entorno Development y la tabla `MenuItems` vacía, cuando arranca la app, entonces se insertan exactamente 3 filas: "Carnes", "Champiñón con pollo" y "Mexicana", todas con `Price = 10500`, `Category = Pizza` e `ImagePath = null`; `IsFeatured = true` en "Carnes" y "Mexicana".
- [ ] Cada pizza tiene una descripción de una frase, marcada en el código con el comentario `// EJEMPLO`.
- [ ] Idempotente: ejecutar el seeder dos veces deja 3 filas; con la tabla no vacía no inserta nada.
- [ ] Fuera de Development el seeder no se ejecuta.
- [ ] Dada la base no disponible al arrancar, el seeder registra un aviso (nivel Warning) y la app arranca igual.

**RF-24 - `MenuService`** (bloque 3; U con SQLite)
- [ ] Devuelve los productos agrupados por categoría (solo las que tienen productos) y ordenados por nombre dentro de cada una.
- [ ] Devuelve las destacadas (`IsFeatured = true`).
- [ ] Devuelve el precio mínimo del menú, o ausencia de valor si no hay productos.
- [ ] Las consultas usan `AsNoTracking()` y son asíncronas.
- [ ] No se ordena por `Price` en la base de datos (SQLite no ordena `decimal`).

**RF-25 - `ContactService`** (bloque 3; U con SQLite)
- [ ] Convierte un `ContactInput` válido en un `ContactMessage` y lo guarda de forma asíncrona.
- [ ] `CreatedAtUtc` toma el valor del `TimeProvider` inyectado: con un reloj de prueba fijado en `2026-10-03T15:00:00Z`, la fila guardada tiene ese instante y `Kind` UTC.
- [ ] Recorta los valores: `Name = "  Ana  "` se guarda como `"Ana"`; lo mismo con `Phone`, `Email` y `Message`.
- [ ] `Phone` o `Email` vacíos o solo con espacios se guardan como `null`.
- [ ] El campo `Website` (honeypot) no se guarda.

### 1.10 Comportamiento con la base de datos caída

**RF-26 - `GET /` sin base de datos** (bloque 6; I)
- [ ] Dado que `MenuService` lanza una excepción, cuando se pide `GET /`, entonces la respuesta es 200.
- [ ] La página muestra una sola vez el texto "El menú no está disponible en este momento. Escríbenos por WhatsApp." en lugar de las secciones de destacadas y menú.
- [ ] Hero (sin sticker "Desde"), "Sobre nosotros", horarios y ubicación, formulario, pie y los enlaces de WhatsApp siguen presentes.
- [ ] La respuesta no contiene traza, nombre de excepción ni detalles de la base.
- [ ] El fallo se registra en el log sin datos personales.

**RF-27 - `POST /` sin base de datos** (bloque 7; I)
- [ ] Dado un POST válido y un fallo al guardar, entonces la respuesta es 200 (sin redirección) con el error general "No pudimos guardar tu mensaje. Inténtalo de nuevo o escríbenos por WhatsApp.".
- [ ] Los valores escritos se conservan en los campos.
- [ ] La respuesta no contiene traza ni detalles de la base, y el log no contiene nombre, teléfono, correo ni mensaje.

**RF-28 - Reintentos acotados** (bloque 2; M)
- [ ] `UseSqlServer` se configura con `EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)`.
- [ ] Con la base local detenida, `GET /` termina respondiendo 200 con el aviso de RF-26 (no queda colgada indefinidamente).

### 1.11 Configuración

**RF-29 - Datos del negocio (`Business`)** (bloque 4b; I, M)
- [ ] `appsettings.json` tiene una sección `Business` con nombre, teléfono, WhatsApp, dirección, horarios y redes, enlazada a `BusinessInfo` con el patrón de opciones (`Configure<BusinessInfo>`).
- [ ] Valores iniciales: `Name = "Harry's Pizza"`, teléfono `3113706576`, WhatsApp `573113706576`, `Address` vacío, `Social` vacío, horarios de RF-14.
- [ ] Dado que la configuración de prueba cambia `Business:Name`, entonces `GET /` muestra el nuevo nombre sin tocar código.
- [ ] La sección no contiene secretos.

**RF-30 - Cadena de conexión** (bloque 2; M, I)
- [ ] La cadena se lee solo de `ConnectionStrings:Default`.
- [ ] Dado que falta o está vacía, cuando arranca la app, entonces se lanza `InvalidOperationException` con un mensaje en español que contiene `ConnectionStrings:Default` y el comando `dotnet user-secrets set "ConnectionStrings:Default" "<cadena>" --project src/Pizzeria`.
- [ ] No hay valor por defecto de la cadena en el código ni en `appsettings*.json`.
- [ ] Las pruebas de integración aportan una cadena ficticia por configuración de prueba y sustituyen el `AppDbContext` por SQLite en memoria.

---

## 2. Requisitos no funcionales

### 2.1 Seguridad

**RNF-01 - Antiforgery** (bloque 7; I)
- [ ] El formulario renderizado incluye el campo oculto `__RequestVerificationToken`.
- [ ] Un POST a `/` sin token responde 400 y no guarda nada.
- [ ] No hay `[IgnoreAntiforgeryToken]` en el proyecto.

**RNF-02 - Validación en servidor** (bloque 3 y 7; U, I)
- [ ] `OnPostAsync` comprueba `ModelState.IsValid` antes de guardar; un POST inválido enviado sin pasar por el navegador (sin atributos HTML5) se rechaza igual (RF-18).
- [ ] Las longitudes máximas de `ContactInput` coinciden con las de las columnas: 80, 20, 254 y 1000.

**RNF-03 - Post/Redirect/Get** (bloque 7; I)
- [ ] Un envío correcto termina siempre en 302 seguido de `GET` (RF-17); recargar la página de confirmación no reenvía el formulario.

**RNF-04 - Honeypot** (bloque 7; I, M)
- [ ] El formulario incluye un campo `Website` oculto por CSS (clase, no atributo `style`), con `tabindex="-1"`, `autocomplete="off"` y dentro de un contenedor con `aria-hidden="true"`.
- [ ] Dado un POST con token y `Website` con valor, entonces no se guarda ninguna fila y la respuesta es la misma que en un envío correcto: 302 a `/#contacto` y "Mensaje enviado".

**RNF-05 - Rate limiting** (bloque 8; I)
- [ ] `AddRateLimiter` con limitador global particionado por IP que solo cuenta peticiones `POST`: 5 por ventana de 10 minutos. `UseRateLimiter()` va después de `UseRouting()`.
- [ ] Dados 5 POST seguidos desde la misma IP, el sexto responde 429 y no guarda nada.
- [ ] Las peticiones `GET` no se limitan: 20 `GET /` seguidos responden 200.

**RNF-06 - Cabeceras de seguridad y CSP** (bloque 8; I)
- [ ] `GET /` y `GET /privacidad` incluyen `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` y `X-Frame-Options: DENY`.
- [ ] Incluyen `Content-Security-Policy` con exactamente estas directivas: `default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`.
- [ ] Solo en Development, y solo si la recarga en caliente lo necesita, se admite añadir `connect-src 'self' ws: wss:`.
- [ ] Con la CSP activa, la consola del navegador no muestra violaciones al cargar `/` en entorno Production local.

**RNF-07 - Sin secretos en el repositorio** (bloques 2 y 8; M)
- [ ] Ni el código, ni `appsettings*.json`, ni los archivos de prueba, ni el historial de commits de la funcionalidad contienen cadenas de conexión reales, contraseñas ni claves.
- [ ] La cadena local vive solo en user-secrets; nunca se imprime en consola ni en logs.

**RNF-08 - Sin datos personales en logs** (bloques 6, 7 y 8; M)
- [ ] Ninguna llamada a `ILogger` recibe `Name`, `Phone`, `Email` ni `Message` de `ContactInput` o `ContactMessage`, ni el objeto completo.
- [ ] Al registrar un fallo de guardado se registra la excepción o su tipo, no los valores del formulario.

**RNF-09 - Salida codificada y errores genéricos** (bloques 4b a 8; M, I)
- [ ] No hay `Html.Raw` en ninguna vista.
- [ ] Dado un POST inválido con `Name = "<script>alert(1)</script>"`, la respuesta contiene el valor codificado (`&lt;script&gt;`) y no la etiqueta literal.
- [ ] Fuera de Development los errores no controlados muestran la página de RF-05.

**RNF-10 - HTTPS, HSTS y dependencias** (bloque 8; M)
- [ ] `UseHttpsRedirection()` y `UseHsts()` (fuera de Development) se conservan de la plantilla.
- [ ] `dotnet list package --vulnerable` no reporta paquetes vulnerables, o los hallazgos se comunican al usuario antes de cerrar el bloque.
- [ ] El checklist del skill `seguridad-web-basica` queda repasado punto por punto en el PR del bloque 8.

**RNF-11 - Sin acciones sobre producción** (todos los bloques; M)
- [ ] No se ejecutan migraciones contra producción ni comandos de despliegue. No se modifica nada fuera del directorio del proyecto.

### 2.2 Accesibilidad

**RNF-12 - Contraste AA** (bloque 9; M con herramienta de contraste)
- [ ] Texto normal: razón de contraste de al menos 4,5:1. Texto grande (24 px o más, o 18,66 px en negrita): al menos 3:1.
- [ ] Se miden y anotan en el PR: crema sobre rojo, marrón sobre crema, marrón sobre amarillo, crema sobre marrón, crema sobre negro, rojo oscuro (`#9E1410`) sobre crema y el color de las descripciones del menú sobre crema.
- [ ] Amarillo sobre rojo se usa solo en titulares grandes (el nombre en el hero); nunca en texto de lectura ni en botones.
- [ ] No se usa rojo sobre amarillo ni rojo sobre marrón para texto.

**RNF-13 - Formulario accesible** (bloque 7 y 9; I, M)
- [ ] Cada campo visible tiene un `<label>` visible encima, asociado por `for`/`id`; no se usa el `placeholder` como etiqueta.
- [ ] Los errores no dependen solo del color: llevan el texto "Error:" delante y están asociados al campo (`aria-describedby` o equivalente).
- [ ] El texto de ayuda del campo "Mensaje" está asociado al campo.
- [ ] La confirmación "Mensaje enviado" y el error general se anuncian a lectores de pantalla (`role="status"` y `role="alert"` respectivamente).

**RNF-14 - Imágenes con `alt`** (bloques 5 y 6; I)
- [ ] Toda `<img>` tiene atributo `alt`. Las de producto: el nombre de la pizza. La ilustración del hero: `alt=""`.

**RNF-15 - Foco visible y teclado** (bloque 9; M)
- [ ] Todos los enlaces, botones y campos se alcanzan con Tab en el orden visual, y se activan con Enter (y Espacio en botones).
- [ ] El foco es siempre visible: contorno de 3 px amarillo y negro en los campos, y un contorno visible equivalente en enlaces y botones sobre cualquier fondo.
- [ ] El campo honeypot no recibe foco con Tab.
- [ ] No hay trampas de foco; la barra fija de móvil no oculta el elemento enfocado.

**RNF-16 - Movimiento reducido** (bloque 9; M)
- [ ] Con `prefers-reduced-motion: reduce` no se ejecuta ninguna animación ni transición (ni el sticker del hero ni el hundimiento de botones).

### 2.3 Rendimiento

**RNF-17 - Sin recursos de terceros** (bloques 4b a 9; I, M)
- [ ] El HTML de `/` y de `/privacidad` no carga ningún recurso (`link`, `script`, `img`, `@font-face`) desde otro origen. Los únicos enlaces externos son de navegación: `wa.me` y, si hay dirección, Google Maps.
- [ ] La pestaña de red del navegador muestra solo peticiones al propio origen.

**RNF-18 - Fuentes** (bloque 4b; M)
- [ ] `wwwroot/fonts/` contiene como máximo dos archivos, ambos `woff2`: Bowlby One (un peso) y Archivo variable (peso y anchura).
- [ ] Peso total objetivo en torno a 100 KB; límite de aceptación 150 KB (supuesto S-6).
- [ ] `@font-face` con `font-display: swap`; solo Bowlby One se precarga (`<link rel="preload" as="font" type="font/woff2" crossorigin>`).
- [ ] Las dos fuentes cubren á, é, í, ó, ú, ñ, ¿ y ¡; si Bowlby One no las cubre, se sustituye por Alfa Slab One y se avisa al usuario.
- [ ] Las licencias (SIL Open Font License) se conservan junto a las fuentes.

**RNF-19 - Imágenes** (bloques 5 y 6; M, I)
- [ ] Las únicas imágenes son SVG propios: `wwwroot/img/pizza-placeholder.svg` y `wwwroot/favicon.svg`. Sin fotos de banco ni recursos con licencia de terceros.
- [ ] Toda `<img>` declara `width` y `height`; las que no son del hero llevan `loading="lazy"`.
- [ ] (Guía para cuando haya fotos reales, no exigible ahora: WebP, 1:1, 800 px de lado, menos de 120 KB.)

**RNF-20 - Página ligera** (bloque 9; M)
- [ ] La página funciona completa con JavaScript desactivado. Si no hace falta ningún script, el HTML no contiene elementos `<script>`.
- [ ] Una sola hoja de estilos propia (`wwwroot/css/site.css`).
- [ ] `GET /` hace como máximo las consultas de lectura de `MenuService`, todas con `AsNoTracking()`.

### 2.4 Responsive

**RNF-21 - Móvil primero** (bloque 9; M)
- [ ] `site.css` define primero los estilos de móvil y amplía con `@media (min-width: ...)`.
- [ ] El layout incluye `<meta name="viewport" content="width=device-width, initial-scale=1">`.
- [ ] A 360, 768 y 1280 px de ancho no hay scroll horizontal ni contenido cortado o solapado (salvo la pizza del hero, cortada a propósito).
- [ ] El contenido tiene un ancho máximo de 1100 px.
- [ ] Destacadas: una columna en móvil, dos en escritorio. Botones del hero: apilados en móvil, en fila en escritorio.
- [ ] Los botones y enlaces principales tienen un área táctil de al menos 44 x 44 px.

### 2.5 Diseño (sección 7 de la propuesta)

**RNF-22 - Paleta** (bloque 4b; M)
- [ ] `site.css` define como variables: `--red: #C8201A`, `--yellow: #FFC72C`, `--cream: #FFF1D0`, `--brown: #3B1F14`, `--black: #000000`, `--red-dark: #9E1410`.
- [ ] Colores planos: `site.css` no usa degradados para colorear fondos (la franja de cuadros es el único patrón) ni transparencias.
- [ ] `--red-dark` se usa solo en mensajes de error del formulario.
- [ ] Fondos por sección: cabecera marrón, hero rojo, destacadas amarillo, menú crema, "Sobre nosotros" marrón, horarios y ubicación crema, contacto rojo con panel crema, pie negro.

**RNF-23 - Tipografía** (bloque 4b y 9; M)
- [ ] Pilas: `"Bowlby One", Impact, "Arial Black", sans-serif` para títulos, nombre del negocio y precios en stickers; `Archivo, system-ui, sans-serif` para lo demás.
- [ ] Escala: 17, 22, 30, 40 y 53 px; base 17 px con interlineado 1,5. Nombre en el hero: `clamp(3.5rem, 14vw, 9rem)` con interlineado 0,9.
- [ ] Bowlby One no se usa por debajo de 22 px ni con negrita simulada (`font-weight: 400`, `font-synthesis: none` o equivalente).
- [ ] Archivo: texto a peso 400, nombres de pizza a 700, precios y navegación en anchura 75 y peso 800.
- [ ] Títulos con mayúscula inicial normal ("Pizzas destacadas"); sin `text-transform: uppercase` en bloques y sin rótulos pequeños encima de los títulos.
- [ ] Precios con `font-variant-numeric: tabular-nums`.
- [ ] Texto de lectura alineado a la izquierda, con un máximo de 65 caracteres por línea (`max-width: 65ch`).

**RNF-24 - Lenguaje visual** (bloques 4b a 6 y 9; M)
- [ ] Botones, tarjetas, paneles y stickers: borde negro de 3 px y sombra `6px 6px 0 #000`. Ninguna sombra con desenfoque en todo el CSS.
- [ ] Radios: 12 px en tarjetas y paneles, 8 px en botones, círculo en stickers. No hay otros valores.
- [ ] Las secciones cambian por color de fondo, sin líneas finas divisorias.
- [ ] La franja de cuadros rojo y crema, hecha con CSS, aparece exactamente dos veces: bajo el hero y sobre el pie.
- [ ] Stickers y sellos solo para precios y para "Destacada", girados entre 4° y 8°. No hay promociones ni descuentos inventados.
- [ ] Composición alineada a la izquierda.
- [ ] Sin texturas de papel, sin iconos decorativos y sin una tercera tipografía.

**RNF-25 - Tratamiento por sección** (bloques 5 a 7 y 9; M)
- [ ] Hero: nombre en amarillo con sombra dura negra; eslogan en crema; botón principal amarillo y secundario con borde crema; la pizza se sale del borde de la sección y el sticker de precio la pisa.
- [ ] Destacadas: tarjetas crema con imagen circular y sticker de precio en la esquina.
- [ ] Menú: tablero de precios en filas (nombre, línea de puntos, precio; descripción debajo), no rejilla de tarjetas.
- [ ] "Sobre nosotros": primera frase a 30 px en crema.
- [ ] Horarios y ubicación: dos etiquetas tipo tique con muescas laterales hechas con CSS.
- [ ] Contacto: panel crema, campos con borde negro.
- [ ] Imagen de marcador: pizza cenital en ilustración plana con los colores de la paleta, contorno negro grueso y halo crema; el mismo dibujo en tarjetas y hero; el favicon es la misma pizza simplificada.

**RNF-26 - Movimiento** (bloque 9; M)
- [ ] Una sola animación al cargar: el sticker de precio del hero entra "estampado" (escala y giro, 400 ms, solo CSS).
- [ ] Los botones se desplazan 2 px al pulsarlos.
- [ ] Ningún elemento aparece o se anima al hacer scroll.

### 2.6 Convenciones de código

**RNF-27 - Convenciones** (todos los bloques; M en la revisión de cada PR)
- [ ] Código, nombres de archivos, clases, propiedades y comentarios técnicos en inglés; todo texto visible para el usuario en español.
- [ ] Entidades en `Models/`; `AppDbContext` en `Data/`; servicios en `Services/`; un parcial por sección en `Pages/Shared/`.
- [ ] Todo acceso a datos es `async`/`await` (`OnGetAsync`, `OnPostAsync`, `ToListAsync`, `SaveChangesAsync`); no hay `.Result` ni `.Wait()`.
- [ ] Dependencias por inyección: `AppDbContext`, `MenuService`, `ContactService`, `TimeProvider` e `IOptions<BusinessInfo>` se reciben por constructor. No se usa `DateTime.Now` ni `DateTime.UtcNow` directamente.
- [ ] Solo LINQ/EF: no hay `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQuery` ni SQL concatenado.
- [ ] Validación con DataAnnotations en `ContactInput`.

### 2.7 Pruebas y entrega

**RNF-28 - Proyecto de pruebas** (bloque 1; M)
- [ ] `tests/Pizzeria.Tests/Pizzeria.Tests.csproj` con xUnit, referencia al proyecto web y paquetes `Microsoft.EntityFrameworkCore.Sqlite` y `Microsoft.AspNetCore.Mvc.Testing`; dado de alta en `Pizzeria.slnx`.
- [ ] `dotnet test` desde la raíz ejecuta las pruebas.
- [ ] Las pruebas no usan SQL Server ni el proveedor InMemory de EF; usan SQLite en memoria.
- [ ] Las pruebas no dependen de user-secrets ni de la red.

**RNF-29 - Cobertura mínima** (bloques 2 a 8; U, I)
- [ ] Existen, como mínimo: `AppDbContextTests`, `PriceFormatterTests`, `ContactInputTests`, `MenuServiceTests`, `ContactServiceTests`, `DbSeederTests` y pruebas de integración para: `GET /` (200, `lang="es"`, nombre), enlace de WhatsApp, ausencia de "Cómo llegar" sin dirección, pizzas sembradas con precio, aviso con el menú caído, POST válido, POST inválido, honeypot, POST sin token (400), cabeceras y sexto POST (429).
- [ ] Las pruebas de integración que hacen POST no se ven afectadas por el límite de RNF-05 (aislamiento por instancia de `WebApplicationFactory` o configuración de prueba).

**RNF-30 - Bloques pequeños y en verde** (todos los bloques; M)
- [ ] Cada bloque es un commit/PR con menos de unas 400 líneas escritas a mano (el 4a se exceptúa: solo borrados de plantilla).
- [ ] Al cerrar cada bloque, `dotnet build` termina sin errores y `dotnet test` en verde.
- [ ] Si una corrección falla dos veces seguidas, se detiene el trabajo y se explica al usuario.

---

## 3. Trazabilidad requisito -> bloque del plan

| Bloque (propuesta, sección 4) | Requisitos funcionales | Requisitos no funcionales |
|---|---|---|
| 0 - Commits previos | (ninguno; preparación) | RNF-07, RNF-30 |
| 1 - Proyecto de pruebas | (ninguno) | RNF-28 |
| 2 - Modelo y contexto | RF-20, RF-21, RF-22, RF-28, RF-30 | RNF-07, RNF-27, RNF-29 |
| 3 - Servicios y seed | RF-12, RF-18 (modelo `ContactInput`), RF-23, RF-24, RF-25, RF-07 (cálculo del mínimo) | RNF-02, RNF-27, RNF-29 |
| 4a - Retirar plantilla | RF-04 | RNF-30 |
| 4b - Layout y base visual | RF-01, RF-02, RF-03, RF-05, RF-29 | RNF-17, RNF-18, RNF-22, RNF-23, RNF-24 |
| 5 - Secciones estáticas | RF-06, RF-08, RF-13, RF-14, RF-15 | RNF-14, RNF-19, RNF-25 |
| 6 - Destacadas y menú | RF-07, RF-09, RF-10, RF-11, RF-26 | RNF-08, RNF-14, RNF-19, RNF-25 |
| 7 - Formulario de contacto | RF-16, RF-17, RF-18, RF-27 | RNF-01, RNF-02, RNF-03, RNF-04, RNF-08, RNF-09, RNF-13 |
| 8 - Seguridad y privacidad | RF-19 | RNF-05, RNF-06, RNF-07, RNF-10 |
| 9 - Repaso final | (ajustes de CSS) | RNF-12, RNF-15, RNF-16, RNF-20, RNF-21, RNF-26 |
| Todos | | RNF-11, RNF-27, RNF-30 |

| Requisito | Bloque | Requisito | Bloque | Requisito | Bloque |
|---|---|---|---|---|---|
| RF-01 | 4b | RF-11 | 6 | RF-21 | 2 |
| RF-02 | 4b | RF-12 | 3 | RF-22 | 2 |
| RF-03 | 4b | RF-13 | 5 | RF-23 | 3 |
| RF-04 | 4a | RF-14 | 5 | RF-24 | 3 |
| RF-05 | 4b | RF-15 | 5 | RF-25 | 3 |
| RF-06 | 5 | RF-16 | 7 | RF-26 | 6 |
| RF-07 | 3 y 6 | RF-17 | 7 | RF-27 | 7 |
| RF-08 | 5 | RF-18 | 3 y 7 | RF-28 | 2 |
| RF-09 | 6 | RF-19 | 8 | RF-29 | 4b |
| RF-10 | 6 | RF-20 | 2 | RF-30 | 2 |

---

## 4. Fuera de alcance (recordatorio de la propuesta)

No son requisitos y no se implementan: pagos, carrito, panel de administración, entidad de reservas, consulta de mensajes desde la web, envío de correos o notificaciones, mapa incrustado, fotos reales, logo gráfico, despliegue a Azure, `ForwardedHeaders` para el proxy de Azure, caché del menú, internacionalización y analítica.

---

## 5. Supuestos adoptados en esta fase

Detalles que la propuesta no fijaba y que se han concretado para poder escribir criterios comprobables. Ninguno amplía el alcance; se pueden cambiar sin rehacer el plan.

- **S-1. Textos de validación.** Los mensajes de la tabla de RF-18 se redactaron aquí. Las etiquetas "Nombre", "Teléfono", "Correo" y "Mensaje" también.
- **S-2. Identificadores de ancla.** `#menu` y `#horarios` (la propuesta solo fijaba `#contacto`).
- **S-3. Orden de categorías.** El del enum (`Pizza`, `Starter`, `Drink`, `Dessert`), que coincide con el del brief. Como la categoría se guarda como texto, el orden se aplica en memoria y no con `ORDER BY` alfabético.
- **S-4. Validación y recorte.** Las longitudes se validan sobre el valor recibido; el recorte (`Trim`) se hace al guardar. Un valor formado solo por espacios cuenta como vacío.
- **S-5. Honeypot.** Se comprueba antes que la validación: si `Website` llega con valor, se responde como éxito aunque el resto de campos sea inválido. Los POST con honeypot también cuentan para el límite de RNF-05.
- **S-6. Peso de fuentes.** La propuesta estima "en torno a 100 KB"; se fija 150 KB como límite de aceptación porque el peso real se conoce al descargarlas.
- **S-7. Rate limiting.** Ventana fija de 10 minutos; cuenta todos los POST (válidos, inválidos y con honeypot).
- **S-8. Destacadas vacías.** Si no hay ningún producto destacado, la sección de destacadas no se muestra (misma regla que las categorías vacías).
- **S-9. Anchos de comprobación.** 360, 768 y 1280 px. El punto de corte exacto entre móvil y escritorio lo decide la implementación.
- **S-10. Fallo sin cadena de conexión.** Se verifica a mano (arranque sin la clave); el plan no prevé una prueba automática para ello.

---

## 6. Dudas abiertas

**No hay dudas que impidan implementar o probar.** Las decisiones 1 a 7 y la verificación del entorno cubren todo lo que estaba pendiente.

Pendientes del usuario que no bloquean la implementación, pero sí la publicación (ya anotados en la propuesta):

1. **Texto del aviso de privacidad (RF-19).** Se entrega un texto orientativo con el nombre del negocio como responsable. Falta tu revisión del texto y el nombre legal del responsable; no es asesoría legal.
2. **Contenido de ejemplo.** Eslogan, "Sobre nosotros" y descripciones de las pizzas quedan marcados como ejemplo (RF-06, RF-13, RF-23) para sustituirlos por el contenido real.
3. **Dirección de diseño.** La sección 7 de la propuesta figura como "pendiente de validación del usuario" en su encabezado, aunque la propuesta completa está aprobada. Aquí se toma como aprobada; si quieres cambiar algo del diseño, afecta a RNF-22 a RNF-26.
