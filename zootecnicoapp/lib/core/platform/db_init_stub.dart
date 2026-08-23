/// Mobile y desktop: `sqflite` ya trae su propio `databaseFactory` nativo
/// (Android/iOS por canal de plataforma). No hay nada que inicializar acá —
/// esta versión existe sólo para que `db_init.dart` tenga una rama por
/// defecto cuando NO se compila para web.
library;

void inicializarFactoryWebSiCorresponde() {}
