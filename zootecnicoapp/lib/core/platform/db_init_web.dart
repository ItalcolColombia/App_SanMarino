/// SOLO para poder correr y validar la app en un navegador durante
/// desarrollo. `sqflite` no tiene backend nativo en web; esto lo reemplaza
/// por el factory que usa IndexedDB. La app real (Android/iOS) nunca
/// compila este archivo — lo selecciona `db_init.dart` por import
/// condicional, sólo cuando el target es web.
library;

import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi_web/sqflite_ffi_web.dart';

void inicializarFactoryWebSiCorresponde() {
  // "NoWebWorker": corre sqlite3-wasm en el hilo principal, sin el
  // SharedWorker (`sqflite_sw.js`). Es la variante simple del paquete — para
  // este uso (validar la app en un navegador durante desarrollo) alcanza, y
  // evita la comunicación por worker que en el dev server de Flutter no
  // termina de conectar.
  databaseFactory = databaseFactoryFfiWebNoWebWorker;
}
