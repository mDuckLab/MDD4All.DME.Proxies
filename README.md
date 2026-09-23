# MDD4All.DME.Proxies

Serializer wrappers for MDD4All.DME, the object graph editor application.

JSON and XML are reached through one shape each, so the rest of the application does not have to know which library is behind them. The dictionary converter is the reason this exists separately: a dictionary with an object as its key has no direct form in JSON, and writing one is a decision rather than a detail.
