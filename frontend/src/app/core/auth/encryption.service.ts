/**
 * Servicio de encriptación para comunicación segura con el backend
 * Usa AES-256-CBC para encriptar/desencriptar datos
 *
 * Compatible con HTTPS (Web Crypto API) y HTTP (crypto-js como fallback)
 */
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import * as CryptoJS from 'crypto-js';

@Injectable({ providedIn: 'root' })
export class EncryptionService {
  // Llave para encriptar datos que se envían al backend (remitente frontend)
  private readonly frontendEncryptionKey: string;

  // Llave para desencriptar datos que vienen del backend (remitente backend)
  private readonly backendDecryptionKey: string;

  constructor() {
    // Obtener llaves del environment
    this.frontendEncryptionKey = environment.encryptionKeys?.remitenteFrontend || '1,2,3,4';
    this.backendDecryptionKey = environment.encryptionKeys?.remitenteBackend || '3,4,5,6';
  }

  /**
   * Encripta datos antes de enviarlos al backend
   * @param data Objeto o string a encriptar
   * @returns String encriptado en base64
   */
  async encryptForBackend<T>(data: T): Promise<string> {
    try {
      const jsonString = typeof data === 'string' ? data : JSON.stringify(data);
      return await this.encrypt(jsonString, this.frontendEncryptionKey);
    } catch (error) {
      console.error('Error al encriptar datos:', error);
      throw new Error('Error al encriptar datos');
    }
  }

  /**
   * Desencripta datos recibidos del backend
   * @param encryptedData String encriptado en base64
   * @returns Objeto desencriptado
   */
  async decryptFromBackend<T>(encryptedData: string): Promise<T> {
    try {
      if (!encryptedData || encryptedData.trim().length === 0) {
        throw new Error('Datos encriptados vacíos o inválidos');
      }

      console.log('🔓 Iniciando desencriptación de respuesta...', {
        length: encryptedData.length,
        preview: encryptedData.substring(0, 50) + '...'
      });

      const decrypted = await this.decrypt(encryptedData, this.backendDecryptionKey);

      if (!decrypted || decrypted.trim().length === 0) {
        throw new Error('Datos desencriptados están vacíos');
      }

      console.log('✅ Desencriptación exitosa, parseando JSON...', {
        length: decrypted.length,
        preview: decrypted.substring(0, 200) + '...'
      });

      const parsed = JSON.parse(decrypted) as T;
      const parsedAny = parsed as any;

      console.log('✅ JSON parseado correctamente', {
        type: typeof parsed,
        allKeys: Object.keys(parsedAny || {}),
        hasToken_camel: !!parsedAny?.token,
        hasToken_Pascal: !!parsedAny?.Token,
        tokenValue_camel: parsedAny?.token,
        tokenValue_Pascal: parsedAny?.Token,
        tokenLength_camel: parsedAny?.token?.length,
        tokenLength_Pascal: parsedAny?.Token?.length,
        hasMenu: !!(parsedAny?.menu || parsedAny?.Menu),
        firstFewChars: JSON.stringify(parsedAny).substring(0, 500) // Primeros 500 chars para ver estructura
      });

      return parsed;
    } catch (error: any) {
      console.error('❌ Error al desencriptar datos del backend:', error);

      // Mensajes de error más específicos
      if (error.message?.includes('base64')) {
        throw new Error('Error al decodificar datos encriptados: formato inválido');
      } else if (error.message?.includes('JSON')) {
        throw new Error('Error al parsear datos desencriptados: formato JSON inválido');
      } else if (error.message?.includes('decrypt')) {
        throw new Error('Error al desencriptar: la llave de desencriptación no coincide');
      }

      throw new Error(`Error al desencriptar datos: ${error.message || 'Error desconocido'}`);
    }
  }

  /**
   * Encripta un string usando AES-256-CBC
   * @param plaintext Texto a encriptar
   * @param key Llave de encriptación (será hasheada a 256 bits)
   * @returns String encriptado en base64
   */
  private async encrypt(plaintext: string, key: string): Promise<string> {
    // Intentar usar Web Crypto API si está disponible (HTTPS/localhost)
    if (this.isWebCryptoAvailable()) {
      try {
        const keyHash = await this.deriveKey(key, 256);
        const iv = this.generateIV();
        return await this.importKeyAndEncrypt(plaintext, keyHash, iv);
      } catch (error) {
        console.warn('⚠️ Error con Web Crypto API, usando crypto-js como fallback:', error);
        // Continuar con crypto-js como fallback
      }
    }

    // Fallback: usar crypto-js (funciona en HTTP y HTTPS)
    return this.encryptWithCryptoJS(plaintext, key);
  }

  /**
   * Encripta usando crypto-js (fallback para HTTP)
   * Formato de salida: IV (16 bytes) + datos encriptados en base64 (igual que Web Crypto API)
   */
  private encryptWithCryptoJS(plaintext: string, key: string): string {
    // Derivar llave usando PBKDF2 (igual que el backend)
    const keyHash = this.deriveKeySync(key, 'sanmarino-salt');

    // Generar IV aleatorio (16 bytes)
    const iv = CryptoJS.lib.WordArray.random(16);

    // Encriptar con AES-256-CBC
    const encrypted = CryptoJS.AES.encrypt(plaintext, keyHash, {
      iv: iv,
      mode: CryptoJS.mode.CBC,
      padding: CryptoJS.pad.Pkcs7
    });

    // Convertir IV y ciphertext a Uint8Array
    const ivWords = iv.words;
    const cipherWords = encrypted.ciphertext.words;
    const ivSigBytes = iv.sigBytes; // 16 bytes
    const cipherSigBytes = encrypted.ciphertext.sigBytes;

    // Crear arrays de bytes
    const ivBytes = new Uint8Array(16);
    for (let i = 0; i < 4; i++) {
      const word = ivWords[i] || 0;
      ivBytes[i * 4] = (word >>> 24) & 0xff;
      ivBytes[i * 4 + 1] = (word >>> 16) & 0xff;
      ivBytes[i * 4 + 2] = (word >>> 8) & 0xff;
      ivBytes[i * 4 + 3] = word & 0xff;
    }

    const cipherBytes = new Uint8Array(cipherSigBytes);
    for (let i = 0; i < cipherWords.length; i++) {
      const word = cipherWords[i] || 0;
      const byteIndex = i * 4;
      if (byteIndex < cipherSigBytes) cipherBytes[byteIndex] = (word >>> 24) & 0xff;
      if (byteIndex + 1 < cipherSigBytes) cipherBytes[byteIndex + 1] = (word >>> 16) & 0xff;
      if (byteIndex + 2 < cipherSigBytes) cipherBytes[byteIndex + 2] = (word >>> 8) & 0xff;
      if (byteIndex + 3 < cipherSigBytes) cipherBytes[byteIndex + 3] = word & 0xff;
    }

    // Combinar IV + datos encriptados
    const combined = new Uint8Array(ivBytes.length + cipherBytes.length);
    combined.set(ivBytes, 0);
    combined.set(cipherBytes, ivBytes.length);

    // Convertir a base64
    return this.arrayBufferToBase64(combined);
  }

  /**
   * Desencripta un string usando AES-256-CBC
   * @param ciphertext Texto encriptado en base64
   * @param key Llave de desencriptación (será hasheada a 256 bits)
   * @returns String desencriptado
   */
  private async decrypt(ciphertext: string, key: string): Promise<string> {
    try {
      // Intentar usar Web Crypto API si está disponible (HTTPS/localhost)
      if (this.isWebCryptoAvailable()) {
        try {
          const keyHash = await this.deriveKey(key, 256);
          const encryptedBytes = new Uint8Array(this.base64ToArrayBuffer(ciphertext));
          const iv = encryptedBytes.slice(0, 16).buffer;
          const encryptedData = encryptedBytes.slice(16).buffer;
          return await this.importKeyAndDecrypt(encryptedData, keyHash, iv);
        } catch (error) {
          console.warn('⚠️ Error con Web Crypto API, usando crypto-js como fallback:', error);
          // Continuar con crypto-js como fallback
        }
      }

      // Fallback: usar crypto-js (funciona en HTTP y HTTPS)
      return this.decryptWithCryptoJS(ciphertext, key);
    } catch (error) {
      console.error('Error en decrypt:', error);
      throw error;
    }
  }

  /**
   * Desencripta usando crypto-js (fallback para HTTP)
   * Formato de entrada: IV (16 bytes) + datos encriptados en base64
   */
  private decryptWithCryptoJS(ciphertext: string, key: string): string {
    try {
      // Decodificar base64
      const encryptedBytes = this.base64ToArrayBuffer(ciphertext);
      const uint8Array = new Uint8Array(encryptedBytes);

      // Extraer IV (primeros 16 bytes)
      const ivBytes = uint8Array.slice(0, 16);

      // Convertir IV a WordArray de crypto-js
      const ivWords: number[] = [];
      for (let i = 0; i < 16; i += 4) {
        const word = (ivBytes[i] << 24) |
                    (ivBytes[i + 1] << 16) |
                    (ivBytes[i + 2] << 8) |
                    ivBytes[i + 3];
        ivWords.push(word);
      }
      const iv = CryptoJS.lib.WordArray.create(ivWords, 16);

      // Extraer datos encriptados (resto)
      const cipherBytes = uint8Array.slice(16);

      // Convertir ciphertext a WordArray de crypto-js
      const cipherWords: number[] = [];
      for (let i = 0; i < cipherBytes.length; i += 4) {
        const end = Math.min(i + 4, cipherBytes.length);
        let word = 0;
        if (end > i) word |= (cipherBytes[i] << 24);
        if (end > i + 1) word |= (cipherBytes[i + 1] << 16);
        if (end > i + 2) word |= (cipherBytes[i + 2] << 8);
        if (end > i + 3) word |= cipherBytes[i + 3];
        cipherWords.push(word);
      }
      const ciphertextWordArray = CryptoJS.lib.WordArray.create(cipherWords, cipherBytes.length);

      // Convertir a formato crypto-js
      const cipherParams = CryptoJS.lib.CipherParams.create({
        ciphertext: ciphertextWordArray
      });

      // Derivar llave usando PBKDF2 (igual que el backend)
      const keyHash = this.deriveKeySync(key, 'sanmarino-salt');

      // Desencriptar
      const decrypted = CryptoJS.AES.decrypt(cipherParams, keyHash, {
        iv: iv,
        mode: CryptoJS.mode.CBC,
        padding: CryptoJS.pad.Pkcs7
      });

      const result = decrypted.toString(CryptoJS.enc.Utf8);

      if (!result) {
        throw new Error('No se pudo desencriptar: resultado vacío (posible llave incorrecta)');
      }

      return result;
    } catch (error: any) {
      console.error('Error en decryptWithCryptoJS:', error);
      throw new Error(`Error al desencriptar con crypto-js: ${error.message || error}`);
    }
  }

  /**
   * Verifica si Web Crypto API está disponible
   * @returns true si crypto.subtle está disponible, false si se debe usar crypto-js
   */
  private isWebCryptoAvailable(): boolean {
    return !!(crypto && crypto.subtle);
  }

  /**
   * Deriva una llave usando PBKDF2 (compatible con el backend)
   * Compatible con Web Crypto API y crypto-js
   */
  private deriveKeySync(keyString: string, salt: string): CryptoJS.lib.WordArray {
    // crypto-js usa PBKDF2 con SHA-256 por defecto
    return CryptoJS.PBKDF2(keyString, salt, {
      keySize: 256 / 32, // 8 palabras (256 bits)
      iterations: 10000
    });
  }

  /**
   * Deriva una llave de 256 bits desde un string usando PBKDF2 (Web Crypto API)
   * Solo se usa cuando crypto.subtle está disponible (HTTPS/localhost)
   */
  private async deriveKey(keyString: string, length: number): Promise<CryptoKey> {
    if (!this.isWebCryptoAvailable()) {
      throw new Error('Web Crypto API no disponible');
    }

    const encoder = new TextEncoder();
    const keyMaterial = await crypto.subtle!.importKey(
      'raw',
      encoder.encode(keyString),
      { name: 'PBKDF2' },
      false,
      ['deriveBits', 'deriveKey']
    );

    const salt = encoder.encode('sanmarino-salt'); // Salt fijo para consistencia

    return crypto.subtle!.deriveKey(
      {
        name: 'PBKDF2',
        salt: salt,
        iterations: 10000,
        hash: 'SHA-256'
      },
      keyMaterial,
      { name: 'AES-CBC', length: length },
      false,
      ['encrypt', 'decrypt']
    );
  }

  /**
   * Genera un IV (Initialization Vector) aleatorio
   * Compatible con Web Crypto API y crypto-js
   */
  private generateIV(): Uint8Array {
    // Intentar usar crypto.getRandomValues (disponible en la mayoría de navegadores)
    if (crypto && crypto.getRandomValues) {
      return crypto.getRandomValues(new Uint8Array(16));
    }

    // Fallback: usar crypto-js para generar IV aleatorio
    const iv = CryptoJS.lib.WordArray.random(16);
    const ivBytes = new Uint8Array(16);
    for (let i = 0; i < 4; i++) {
      const word = iv.words[i] || 0;
      ivBytes[i * 4] = (word >>> 24) & 0xff;
      ivBytes[i * 4 + 1] = (word >>> 16) & 0xff;
      ivBytes[i * 4 + 2] = (word >>> 8) & 0xff;
      ivBytes[i * 4 + 3] = word & 0xff;
    }
    return ivBytes;
  }

  /**
   * Importa la llave y encripta los datos
   * Formato: IV (16 bytes) + datos encriptados, todo en un solo base64
   */
  private async importKeyAndEncrypt(plaintext: string, key: CryptoKey, iv: Uint8Array): Promise<string> {
    if (!this.isWebCryptoAvailable()) {
      throw new Error('Web Crypto API no disponible');
    }

    const encoder = new TextEncoder();
    const data = encoder.encode(plaintext);

    const encrypted = await crypto.subtle!.encrypt(
      { name: 'AES-CBC', iv: iv },
      key,
      data
    );

    // Combinar IV y datos encriptados en un solo ArrayBuffer
    const combined = new Uint8Array(iv.length + encrypted.byteLength);
    combined.set(iv, 0);
    combined.set(new Uint8Array(encrypted), iv.length);

    // Convertir a base64 (formato que espera el backend)
    return this.arrayBufferToBase64(combined);
  }

  /**
   * Importa la llave y desencripta los datos
   */
  private async importKeyAndDecrypt(encryptedData: ArrayBuffer, key: CryptoKey, iv: ArrayBuffer): Promise<string> {
    if (!this.isWebCryptoAvailable()) {
      throw new Error('Web Crypto API no disponible');
    }

    const decrypted = await crypto.subtle!.decrypt(
      { name: 'AES-CBC', iv: iv },
      key,
      encryptedData
    );

    const decoder = new TextDecoder();
    return decoder.decode(decrypted);
  }

  /**
   * Convierte ArrayBuffer a Base64
   */
  private arrayBufferToBase64(buffer: ArrayBuffer | Uint8Array): string {
    const bytes = buffer instanceof Uint8Array ? buffer : new Uint8Array(buffer);
    let binary = '';
    bytes.forEach(byte => {
      binary += String.fromCharCode(byte);
    });
    return btoa(binary);
  }

  /**
   * Convierte Base64 a ArrayBuffer
   */
  private base64ToArrayBuffer(base64: string): ArrayBuffer {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
  }

  /**
   * Encripta el SECRET_UP usando la llave de encriptación específica para SECRET_UP
   * @param secretUp El SECRET_UP en texto plano
   * @returns El SECRET_UP encriptado en base64
   */
  async encryptSecretUp(secretUp: string): Promise<string> {
    try {
      const encryptionKey = environment.platformSecret?.encryptionKey;
      if (!encryptionKey) {
        throw new Error('SECRET_UP encryption key no configurada en environment');
      }

      return await this.encrypt(secretUp, encryptionKey);
    } catch (error) {
      console.error('Error al encriptar SECRET_UP:', error);
      throw new Error('Error al encriptar SECRET_UP');
    }
  }
}

