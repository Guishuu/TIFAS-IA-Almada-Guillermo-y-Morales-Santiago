# Candil — asistente de soporte con RAG para *Cenizas del Faro*

Producto de IA del estudio ficticio **Tero Nocturno Games** (TIFAS – Inteligencia Artificial, UCP).

**Integrantes:** Almada Guillermo Benjamín · Morales Santiago
Chatbot de soporte dentro del juego, hecho en Unity (C#), sin paquetes externos.

> **Estado:** los scripts compilan y se ejecutaron en Unity. La lógica de decisión también se probó con una
> réplica en Python (`Pruebas/replica.py`). Falta medir las configuraciones A y B con Ollama encendido.

## Qué hace
1. **Valida** la entrada (vacía, muy larga, insultos, intentos de manipular el prompt, límite de consultas).
2. **Responde preguntas frecuentes** con texto predefinido, sin llamar al modelo.
3. **Clasifica la intención** con Naive Bayes entrenado con `intenciones.json`.
4. **Recupera** los 3 documentos más relevantes con BM25 (la "R" de RAG).
5. Si no hay evidencia suficiente, **no inventa**: registra la consulta y **escala a una persona**.
6. Si hay evidencia, el **modelo de lenguaje redacta** la respuesta usando solo esos documentos.
7. Si el modelo no responde (sin conexión), devuelve el documento mejor puntuado (**modo extractivo**).
8. **Registra** todo en CSV: entrada, intención, documentos, ruta, tokens, latencia, costo, error y respuesta.

## Instalación
1. Unity 2021.3 LTS o superior (probar con la versión que use el equipo). Crear un proyecto 2D o 3D vacío.
2. Copiar las carpetas `Candil` y `StreamingAssets` **directamente en la raíz** de `Assets` (desde la ventana Project de Unity).
   Si quedan en `Assets/Assets/...`, Unity no encuentra la base de conocimiento y todas las consultas salen como INVALIDO.
3. En una escena vacía: `GameObject > Create Empty`, renombrarlo "Candil" y agregarle el componente **CandilChatUI**
   (agrega solo el **CandilOrchestrator**).
4. Instalar [Ollama](https://ollama.com), y en una terminal: `ollama pull llama3.2:3b` (queda escuchando en `localhost:11434`).
5. Presionar Play.

Sin Ollama el producto igual funciona: FAQ, saludos, tickets y escalado no usan el modelo, y las demás
consultas caen al modo extractivo. Sirve como plan B si falla algo durante la defensa.

## Configuración (Inspector del componente CandilOrchestrator)
| Campo | Para qué sirve | Valor inicial |
|---|---|---|
| mode | Configuración A (sin optimizar) o B (optimizada) | B |
| provider / model | Ollama local o API compatible con OpenAI | Ollama, `llama3.2:3b` |
| faqThreshold | Similitud mínima para responder con una FAQ | 0,6 |
| retrievalThreshold | Puntaje BM25 mínimo; por debajo se escala | 2,7 |
| intentThreshold | Confianza mínima del clasificador | 0,4 |
| topK | Documentos que se envían al modelo en B | 3 |
| maxOutputTokensB / historyTurnsB | Control de salida y límite de contexto | 160 / 2 |
| priceInput / priceCachedInput / priceOutput | Tarifas para calcular el costo equivalente | 0,10 / 0,01 / 0,50 |

## Variables de entorno
- `CANDIL_API_KEY`: clave de la API. Solo se necesita con `provider = ApiCompatibleOpenAI`.
  **Nunca** se escribe en el código ni se sube al repositorio.
- Si el proveedor rechaza el campo `max_tokens`, renombrarlo en `OpenAiRequest` (archivo `LlmClient.cs`)
  por el que indique su documentación.

## Dónde quedan los logs
`Application.persistentDataPath/CandilLogs/` (la ruta exacta se ve activando "Métricas" en el chat):
- `interacciones.csv`: una fila por consulta.
- `feedback.csv`: botones "Me sirvió / No me sirvió".
- `tickets.csv`: reportes de bugs y consultas escaladas a una persona.

## Cómo medir la comparación A vs B
1. Con el botón de arriba elegir **Config. A**, hacer las preguntas de `Pruebas/pruebas.json`.
2. Cambiar a **Config. B** y repetir las mismas preguntas.
3. Abrir `interacciones.csv`, promediar tokens, latencia y costo por configuración y cargar los valores
   reales en la planilla de costos (reemplazan a los estimados).

## Limitaciones conocidas
- La recuperación es léxica: no entiende sinónimos que no estén en los documentos ("joystick de play", "se cuelga").
  En esos casos escala a una persona en lugar de responder. Mejora futura: embeddings o lista de sinónimos.
- El clasificador se entrenó con unos 70 ejemplos: la confianza es baja con frases muy distintas a las vistas.
- Solo PC (lee archivos de StreamingAssets con `System.IO`); en Android o WebGL hay que cargar los JSON con `UnityWebRequest`.
- En producción el juego no debe llamar directo a una API paga: la clave quedaría expuesta. Debe pasar por un
  servidor intermedio del estudio.
- Las listas de palabras bloqueadas y de patrones de manipulación son mínimas, de demostración.

## Estructura
```
Assets/Candil/Scripts/
  CandilChatUI.cs          Interfaz (IMGUI)
  CandilOrchestrator.cs    Orquestador: validar -> decidir -> actuar -> registrar
  FaqCache.cs              Respuestas predefinidas (Jaccard)
  NaiveBayesClassifier.cs  Clasificador de intención (aprendizaje supervisado)
  Bm25Retriever.cs         Recuperación con función heurística BM25
  LlmClient.cs             Cliente HTTP del modelo (Ollama o API)
  InteractionLogger.cs     Logs, tickets, feedback y métricas
  KnowledgeBase.cs         Carga de los JSON
  TextUtil.cs              Normalización y tokenización
Assets/StreamingAssets/Candil/
  conocimiento.json  faq.json  intenciones.json
Pruebas/
  replica.py  pruebas.json  pruebas_nuevas.json
```


## Entregables del TIFAS
| Entregable | Dónde |
|---|---|
| Código del producto (Unity, C#) | `Assets/` |
| Pruebas de la lógica (40 casos) | `Pruebas/` |
| Guion de la defensa oral | `docs/Guion_Defensa_Oral_Candil.pdf` |
| Logos de Tero Nocturno Games | `docs/` y raíz |
| Informe, planilla de costos y presentación | [completar con los enlaces o agregar los archivos exportados a `docs/`] |
