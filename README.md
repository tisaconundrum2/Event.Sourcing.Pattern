# Event.Sourcing.Pattern
Instead of storing just the current state of the data in a relational database, store the full series of actions taken on an object in an append-only store. The store acts as the system of record and can be used to materialize the domain objects. This approach can improve performance, scalability, and auditability in complex systems.
