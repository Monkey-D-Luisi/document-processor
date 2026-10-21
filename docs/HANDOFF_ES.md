# Entrega — Caso 2: Document Processor

## Estado

La aplicación compila y se ha probado localmente con PostgreSQL y con ambos
modelos de OpenAI. El modelo predeterminado es `gpt-6-luna`; `gpt-5.6-luna`
sigue disponible mediante configuración. La comparación utilizó exclusivamente
documentos sintéticos. No se ha publicado ni desplegado nada.

El código propio del proyecto se licencia bajo MIT, con titular `Monkey-D-Luisi`
y año 2026. MediatR 12.5.0 mantiene su licencia Apache-2.0 independiente,
declarada en los metadatos oficiales de NuGet; la licencia MIT del proyecto no
la sustituye. Esta combinación no presenta un bloqueo de licencia identificado
para publicar el código del proyecto. MediatR 12 y anteriores no reciben
garantías de soporte ni correcciones del mantenedor, por lo que mantener esta
versión implica aceptar y vigilar ese riesgo.

## Verificación realizada el 21/10/2026

- `dotnet restore .\DocumentProcessor.sln`: correcto.
- `dotnet build .\DocumentProcessor.sln --configuration Release`: correcto,
  cero warnings y errores.
- `dotnet test .\DocumentProcessor.sln --configuration Release`: 38 pruebas
  correctas.
- `node --check .\src\DocumentProcessor.API\wwwroot\app.js`: correcto.
- `dotnet list .\DocumentProcessor.sln package --vulnerable
  --include-transitive`: no encontró paquetes vulnerables.
- La compilación Release terminó con 0 advertencias y 0 errores; los cuatro
  proyectos de tests pasaron 38/38 pruebas.
- MediatR 12.5.0 compila y pasa las pruebas en `net10.0`; su paquete declara
  compatibilidad desde .NET 6 / .NET Standard 2.0. El grafo restaurado no
  contiene MediatR 13 ni 14.
- Docker PostgreSQL está saludable. La migración de Development se aplicó y
  `/health` devolvió HTTP 200. La base tiene 10 documentos `Completed` y un
  documento `Failed` de la primera subida de imagen.
- La primera subida de imagen falló localmente porque faltaba el tipo MIME al
  construir `BinaryData`; la solicitud no llegó a OpenAI y no generó tokens.
  Se corrigió el adaptador y hay pruebas de regresión para PNG, JPEG y WebP.
- Se completaron 10 respuestas reales de OpenAI, cinco por modelo, con los
  mismos cinco documentos sintéticos. No hubo reintentos del proveedor. La
  subida local fallida explica que el endpoint recibiera 11 envíos en total.
- La UI mostró desde PostgreSQL el resultado guardado de la factura
  inconsistente, incluidos el aviso de revisión, modelo, latencia, tokens y
  coste estimado. No hubo errores ni avisos de consola en el navegador. El
  manejador del formulario también se probó con respuestas de red simuladas:
  verificó antiforgery, multipart y renderizado sin otra llamada al proveedor.
- Ambos modelos alcanzaron 108/108 comprobaciones exactas del ground truth. El
  informe, los documentos sintéticos, el ground truth y las respuestas
  anonimizadas por diseño están en
  [`MODEL-EVALUATION-2026-10-21.md`](MODEL-EVALUATION-2026-10-21.md) y
  `artifacts/model-evaluation-2026-10-21/`.

El coste registrado es una estimación local basada en los tokens devueltos y
las tarifas configuradas; no es un extracto de facturación de la cuenta.

## Decisión de modelo

| Modelo | Acierto en la muestra | Latencia mediana | Coste estimado / 5 documentos |
| --- | ---: | ---: | ---: |
| `gpt-5.6-luna` | 108/108 | 3.095 ms | $0.003601 |
| `gpt-6-luna` | 108/108 | 3.602 ms | $0.001769 |

GPT-6 Luna costó un 50,9 % menos y tuvo una latencia mediana un 16,4 % mayor.
Los cinco documentos son una muestra pequeña: no demuestra una superioridad
general de calidad o inteligencia. Se eligió GPT-6 Luna como predeterminado
porque igualó los resultados observados con menor coste, según la preferencia
del propietario del proyecto.

## Ejecución exacta en Windows

Requisitos: .NET 10 SDK y Docker Desktop. Desde PowerShell, en la raíz del
repositorio:

```powershell
docker compose up -d
dotnet restore .\DocumentProcessor.sln
dotnet build .\DocumentProcessor.sln --configuration Release
dotnet test .\DocumentProcessor.sln --configuration Release

$keyFile = Join-Path $env:USERPROFILE 'secrets\openai_api_key.txt'
$env:OpenAI__ApiKey = (Get-Content -Raw $keyFile).Trim()
Remove-Item Env:OpenAI__Model -ErrorAction SilentlyContinue
try {
    dotnet run --project .\src\DocumentProcessor.API\DocumentProcessor.API.csproj --urls http://127.0.0.1:5178
}
finally {
    Remove-Item Env:OpenAI__ApiKey -ErrorAction SilentlyContinue
    Remove-Item Env:OpenAI__Model -ErrorAction SilentlyContinue
}
```

Abre <http://127.0.0.1:5178>. En Development la API aplica migraciones
pendientes al arrancar. La clave se lee del fichero externo y no debe copiarse
al repositorio ni imprimirse. Cada documento que se suba puede generar una
petición facturable a OpenAI.

Para usar GPT-5.6 Luna en esa sesión, asigna
`$env:OpenAI__Model = 'gpt-5.6-luna'` antes de ejecutar `dotnet run`. Los dos
modelos y sus tarifas permanecen en
[`src/DocumentProcessor.API/appsettings.json`](../src/DocumentProcessor.API/appsettings.json).

Endpoints: `GET /api/v1/documents/antiforgery`,
`POST /api/v1/documents/process`, `GET /api/v1/documents/{id}` y `GET /health`.
La UI gestiona antiforgery y muestra datos extraídos, líneas, coherencias,
estado de revisión y telemetría.

## Pendiente antes de hacerlo público o mostrarlo a clientes

1. Grabar una demo corta con documentos sintéticos y revisar el flujo visual
   desde el selector hasta el resultado.
2. Repetir la comparación con un conjunto más amplio y representativo antes de
   hacer afirmaciones comerciales sobre calidad, velocidad o ahorro.
3. Añadir autenticación, autorización, retención/borrado y controles de
   operación antes de exponer la aplicación a terceros o cargar documentos
   reales.
4. Aceptar explícitamente el riesgo de mantener MediatR 12.5.0 sin soporte ni
   correcciones del fabricante y vigilar avisos de seguridad. Si ese riesgo
   deja de ser aceptable, valorar una alternativa mantenida antes de actualizar
   a MediatR 13 o posterior, cuya licencia debe revisarse por separado.
5. Confirmar que el nombre humano mostrado como autor del commit inicial es
   intencional antes de publicar el historial. El email del commit es una
   dirección `noreply` de GitHub.

La auditoría de preparación pública y su checklist están en
[`PUBLIC-RELEASE-READINESS.md`](PUBLIC-RELEASE-READINESS.md). La comprobación
de seguridad gestionada quedó pendiente porque el entorno de escaneo no pudo
iniciar un worker de solo lectura sin un perfil de permisos administrado.

Antiforgery protege el formulario del navegador frente a CSRF, pero no
autentica usuarios. La API y su endpoint de lectura deben permanecer locales
hasta añadir controles de acceso.

No se añaden n8n, email ni integraciones ERP; quedan fuera del Caso 2.
