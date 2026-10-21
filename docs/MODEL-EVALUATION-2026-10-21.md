# Evaluación de GPT-5.6 Luna y GPT-6 Luna

Fecha: 21/10/2026. Entorno: build Release local, PostgreSQL en Docker, API de
Responses real y cinco documentos sintéticos españoles. Se enviaron los mismos
cinco archivos a cada modelo en orden alternado: factura PDF, factura como PNG,
factura de varias líneas, factura JPEG con total incoherente y pedido de compra
que no es factura.

## Resultados

| Modelo | Respuestas completadas | Ground truth exacto | Latencia mediana | Tokens de entrada | Tokens de salida | Coste estimado |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `gpt-5.6-luna` | 5 | 108/108 | 3.095 ms | 10.367 | 959 | $0.003601 |
| `gpt-6-luna` | 5 | 108/108 | 3.602 ms | 10.367 | 1.089 | $0.001769 |

La latencia mediana de GPT-6 Luna fue un 16,4 % mayor en esta ejecución. Su
coste estimado para los cinco documentos fue un 50,9 % menor. Los dos modelos
devolvieron el identificador configurado en las cinco respuestas.

Cada conjunto tuvo 108 comprobaciones exactas: 13 campos de cabecera y revisión
por documento, cantidad de líneas, seis campos por línea y códigos de incidencia
obligatorios. Ambos extrajeron correctamente los datos comprobados, señalaron
para revisión la factura con total incoherente e identificaron el pedido como
`Unknown`. En una de las imágenes GPT-6 Luna añadió un aviso descriptivo; el
código obligatorio `total_mismatch` también estuvo presente.

## Incidencia durante la prueba

La primera subida de imagen falló dentro del proceso local con
`ArgumentNullException (MediaType)`: el adaptador creaba `BinaryData` sin tipo
MIME, requisito del SDK de .NET al construir una imagen. La excepción ocurrió
antes de llamar al cliente de OpenAI, con latencia de proveedor de 0 ms y sin
tokens. Se pasó el tipo MIME validado a `BinaryData` y se añadieron tres pruebas
de regresión para PNG, JPEG y WebP. Después se completaron las diez respuestas
reales autorizadas. Por ello hubo once envíos al endpoint local, pero solo diez
solicitudes al proveedor.

## Método y límites

El ground truth se revisó frente a los cinco originales sintéticos. La
comparación usó el mismo build, prompt, esquema JSON, políticas de validación y
datos por modelo. La latencia es la medida por el adaptador desde que construye
la solicitud hasta que recibe la respuesta; no incluye persistencia en
PostgreSQL ni renderizado de UI. El coste combina el uso real de tokens
devuelto por cada respuesta con las tarifas configuradas para el modelo,
incluidos los tokens de escritura/lectura de caché; es una estimación, no el
importe de facturación de OpenAI. Las tarifas configuradas se contrastaron con la
[documentación oficial de GPT-5.6 Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna),
[GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna) y la
[página de precios de API](https://developers.openai.com/api/docs/pricing).

Cinco documentos no representan la variación real de proveedores, escaneos,
idiomas, formatos ni casos contables. El resultado no prueba que GPT-6 Luna sea
más inteligente o más preciso en general; solo muestra igualdad exacta en esta
muestra, menor coste estimado y mayor latencia observada.

## Decisión

Se establece `gpt-6-luna` como predeterminado, manteniendo ambos modelos
configurables. La decisión responde a la preferencia del propietario del
proyecto y a los resultados observados: precisión idéntica en esta muestra y
menor coste, aceptando una mediana más lenta. Para revertir temporalmente a
GPT-5.6 Luna, configura `OpenAI__Model=gpt-5.6-luna`.

Los documentos de prueba, el ground truth corregido y las respuestas con
telemetría están en
[`../artifacts/model-evaluation-2026-10-21/`](../artifacts/model-evaluation-2026-10-21/).
