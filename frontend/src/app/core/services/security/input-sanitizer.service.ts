// src/app/core/services/security/input-sanitizer.service.ts
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class InputSanitizerService {
  // Patrones de inyección SQL comunes
  // NOTA: No incluir @, ., -, _ en los patrones básicos ya que son válidos en emails
  private readonly sqlInjectionPatterns = [
    /(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|SCRIPT)\b)/gi,
    /(--|;|\*|'|"|`|%|\||&|!|#|\$|\\|\/|\?|>|<|=)/gi, // Removido @ de este patrón básico
    /(OR|AND)\s+\d+\s*=\s*\d+/gi,
    /(OR|AND)\s+['"]?\w+['"]?\s*=\s*['"]?\w+['"]?/gi,
    /(UNION\s+(ALL\s+)?SELECT)/gi,
    /(INSERT\s+INTO)/gi,
    /(UPDATE\s+\w+\s+SET)/gi,
    /(DELETE\s+FROM)/gi,
    /(DROP\s+(TABLE|DATABASE|INDEX|VIEW))/gi,
    /(CREATE\s+(TABLE|DATABASE|INDEX|VIEW|PROCEDURE|FUNCTION))/gi,
    /(ALTER\s+(TABLE|DATABASE|INDEX|VIEW))/gi,
    /(EXEC(UTE)?\s*\()/gi,
    /(xp_|sp_)/gi, // SQL Server stored procedures
    /(script\s*:)/gi,
    /(javascript\s*:)/gi,
    /(on\w+\s*=)/gi, // Event handlers like onclick=
    /(<script|<\/script>)/gi,
    /(eval\s*\()/gi,
    /(expression\s*\()/gi,
    /(vbscript\s*:)/gi,
    /(iframe|object|embed)/gi,
    /(base64)/gi,
    /(data\s*:)/gi
  ];

  // Caracteres peligrosos que deben ser eliminados (solo los más peligrosos)
  // NO incluir @, ., -, _ ya que son válidos en emails y contraseñas
  private readonly dangerousChars = /[<>'";\\/&|`]/g;

  /**
   * Valida si un string contiene patrones de inyección SQL
   * @param input El string a validar
   * @returns true si contiene patrones peligrosos, false si es seguro
   */
  containsSqlInjection(input: string | null | undefined): boolean {
    if (!input || typeof input !== 'string') {
      return false;
    }

    // Para emails, validar de forma más permisiva (solo patrones SQL reales, no caracteres comunes)
    if (this.isEmail(input)) {
      // Solo buscar comandos SQL reales que puedan ser peligrosos en un contexto SQL
      // NO buscar caracteres comunes como @, ., -, _, etc.
      // Para emails, solo buscar comandos SQL completos, no caracteres individuales
      const emailSqlPatterns = [
        // Comandos SQL peligrosos (deben estar como palabras completas)
        /\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|SCRIPT)\b/gi,
        // Comentarios SQL (dos guiones seguidos)
        /--/g,
        // Punto y coma seguido de espacio o al final (puede ser peligroso en SQL)
        /;\s/gi,
        /;$/gi,
        // Comillas simples o dobles seguidas de operadores SQL
        /['"]\s*(OR|AND|UNION)\s*['"]/gi,
        // Operadores SQL con condiciones sospechosas (OR/AND con igualdades)
        /\b(OR|AND)\s+\d+\s*=\s*\d+/gi,
        /\b(OR|AND)\s+['"]?\w+['"]?\s*=\s*['"]?\w+['"]?/gi,
        // UNION SELECT
        /\bUNION\s+(ALL\s+)?SELECT\b/gi,
        // INSERT INTO
        /\bINSERT\s+INTO\b/gi,
        // UPDATE SET
        /\bUPDATE\s+\w+\s+SET\b/gi,
        // DELETE FROM
        /\bDELETE\s+FROM\b/gi,
        // DROP statements
        /\bDROP\s+(TABLE|DATABASE|INDEX|VIEW)\b/gi,
        // CREATE statements
        /\bCREATE\s+(TABLE|DATABASE|INDEX|VIEW|PROCEDURE|FUNCTION)\b/gi,
        // ALTER statements
        /\bALTER\s+(TABLE|DATABASE|INDEX|VIEW)\b/gi,
        // EXEC statements
        /\bEXEC(UTE)?\s*\(/gi,
        // SQL Server stored procedures
        /\b(xp_|sp_)\w+/gi
      ];

      for (const pattern of emailSqlPatterns) {
        if (pattern.test(input)) {
          return true;
        }
      }

      // Verificar combinaciones sospechosas en emails (solo si contienen comandos SQL reales)
      // Solo buscar patrones que claramente indiquen inyección SQL, no caracteres comunes
      const emailSuspicious = [
        // Comillas seguidas de operadores SQL (patrones claros de inyección)
        /'[\s]*OR[\s]*'/gi,
        /"[\s]*OR[\s]*"/gi,
        /'[\s]*AND[\s]*'/gi,
        /"[\s]*AND[\s]*"/gi,
        /'[\s]*UNION[\s]*'/gi,
        /"[\s]*UNION[\s]*"/gi,
        // Igualdades sospechosas con comillas
        /'[\s]*=[\s]*'/gi,
        /"[\s]*=[\s]*"/gi
      ];

      for (const pattern of emailSuspicious) {
        if (pattern.test(input)) {
          return true;
        }
      }

      return false;
    }

    // Para otros campos, validación completa
    const normalizedInput = input.trim().toUpperCase();

    // Verificar cada patrón
    for (const pattern of this.sqlInjectionPatterns) {
      if (pattern.test(normalizedInput)) {
        return true;
      }
    }

    // Verificar combinaciones sospechosas
    const suspiciousCombinations = [
      /'.*OR.*'/gi,
      /".*OR.*"/gi,
      /'.*AND.*'/gi,
      /".*AND.*"/gi,
      /'.*UNION.*'/gi,
      /".*UNION.*"/gi,
      /\d+\s*=\s*\d+/gi, // números iguales
      /'\s*=\s*'/gi, // comillas simples iguales
      /"\s*=\s*"/gi // comillas dobles iguales
    ];

    for (const pattern of suspiciousCombinations) {
      if (pattern.test(input)) {
        return true;
      }
    }

    return false;
  }

  /**
   * Sanitiza un string eliminando caracteres peligrosos
   * @param input El string a sanitizar
   * @returns El string sanitizado
   */
  sanitize(input: string | null | undefined): string {
    if (!input || typeof input !== 'string') {
      return '';
    }

    // Para emails, solo eliminar patrones peligrosos pero mantener todos los caracteres válidos de email
    if (this.isEmail(input)) {
      // Eliminar solo patrones peligrosos (scripts, XSS) pero mantener estructura del email
      let sanitized = input
        .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '') // Eliminar scripts
        .replace(/<[^>]+>/g, '') // Eliminar HTML tags
        .replace(/javascript:/gi, '') // Eliminar javascript:
        .replace(/on\w+\s*=/gi, '') // Eliminar event handlers
        .trim();

      // Permitir caracteres válidos de email: letras, números, @, ., _, -, +
      // NO eliminar caracteres válidos del email
      return sanitized;
    }

    // Para otros campos (como contraseñas), eliminar solo patrones peligrosos pero mantener caracteres especiales necesarios
    let sanitized = input
      .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '') // Eliminar scripts
      .replace(/<[^>]+>/g, '') // Eliminar HTML tags
      .replace(/javascript:/gi, '') // Eliminar javascript:
      .replace(/on\w+\s*=/gi, '') // Eliminar event handlers
      .trim();

    // Para contraseñas y otros campos, solo eliminar caracteres muy peligrosos (no eliminar @, ., etc.)
    // Eliminar solo: < > ' " ; \ / & | `
    sanitized = sanitized.replace(/[<>'";\\/&|`]/g, '');

    return sanitized;
  }

  /**
   * Valida un objeto completo buscando inyecciones SQL en todas sus propiedades
   * @param obj El objeto a validar
   * @returns Un objeto con el resultado de la validación
   */
  validateObject(obj: any): { isValid: boolean; errors: string[]; field?: string } {
    const errors: string[] = [];

    if (!obj || typeof obj !== 'object') {
      return { isValid: false, errors: ['Objeto inválido'] };
    }

    // Campos que NO deben ser validados (pueden contener cualquier carácter)
    // Incluye tokens de reCAPTCHA (son cadenas firmadas de Google y pueden traer símbolos)
    const excludedFields = [
      'password', 'Password', 'currentPassword', 'newPassword', 'confirmPassword', 'oldPassword',
      'recaptchaToken', 'g-recaptcha-response', 'gRecaptchaResponse', 'recaptcha'
    ];

    // Validar cada propiedad del objeto
    for (const [key, value] of Object.entries(obj)) {
      // Saltar validación para campos de contraseña (pueden contener cualquier carácter)
      if (excludedFields.includes(key)) {
        continue;
      }

      if (typeof value === 'string') {
        if (this.containsSqlInjection(value)) {
          errors.push(`Campo '${key}' contiene patrones sospechosos de inyección SQL`);
          return { isValid: false, errors, field: key };
        }
      } else if (typeof value === 'object' && value !== null) {
        // Validar objetos anidados recursivamente
        const nestedValidation = this.validateObject(value);
        if (!nestedValidation.isValid) {
          errors.push(...nestedValidation.errors);
          return { isValid: false, errors, field: nestedValidation.field || key };
        }
      }
    }

    return { isValid: errors.length === 0, errors };
  }

  /**
   * Sanitiza un objeto completo
   * @param obj El objeto a sanitizar
   * @returns El objeto sanitizado
   */
  sanitizeObject<T>(obj: T): T {
    if (!obj || typeof obj !== 'object') {
      return obj;
    }

    // Campos que NO deben ser sanitizados (contraseñas/tokens pueden contener cualquier carácter)
    // Incluye tokens de reCAPTCHA para no alterarlos
    const excludedFields = [
      'password', 'Password', 'currentPassword', 'newPassword', 'confirmPassword', 'oldPassword',
      'recaptchaToken', 'g-recaptcha-response', 'gRecaptchaResponse', 'recaptcha'
    ];

    const sanitized = { ...obj } as any;

    for (const [key, value] of Object.entries(obj)) {
      // No sanitizar campos de contraseña (mantener tal cual)
      if (excludedFields.includes(key)) {
        sanitized[key] = value;
        continue;
      }

      if (typeof value === 'string') {
        sanitized[key] = this.sanitize(value);
      } else if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
        sanitized[key] = this.sanitizeObject(value);
      } else if (Array.isArray(value)) {
        sanitized[key] = value.map(item =>
          typeof item === 'string' ? this.sanitize(item) : item
        );
      }
    }

    return sanitized;
  }

  /**
   * Verifica si un string parece ser un email válido
   */
  private isEmail(input: string): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(input);
  }
}

