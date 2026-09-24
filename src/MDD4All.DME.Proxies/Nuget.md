Reads and writes an object graph as JSON or XML.

Both formats are reached through one shape each, so the rest of an application does not
have to know which library is behind them, and swapping one out touches a single place.

The dictionary converter is why this exists separately. JSON has no form for a
dictionary whose key is an object - a key there is always a string. Writing one down
anyway is a decision rather than a detail, and this is where that decision lives.

Targets netstandard2.0.
