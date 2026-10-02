import { base64Puro, base64ABlob } from './base64-a-blob.funcion';

// "%PDF-1.4" en base64.
const PDF = 'JVBERi0xLjQ=';

describe('base64Puro', () => {
  it('base64 puro vuelve igual', () => expect(base64Puro(PDF)).toBe(PDF));

  it('data URL pierde el prefijo', () => {
    expect(base64Puro(`data:application/pdf;base64,${PDF}`)).toBe(PDF);
    expect(base64Puro(`DATA:;base64,${PDF}`)).toBe(PDF);
  });

  it('quita espacios y saltos de linea', () => expect(base64Puro(' JVBE\r\nRi0x\nLjQ= ')).toBe(PDF));

  it('null o vacio da cadena vacia', () => {
    expect(base64Puro(null)).toBe('');
    expect(base64Puro(undefined)).toBe('');
    expect(base64Puro('data:application/pdf;base64')).toBe('');
  });
});

describe('base64ABlob', () => {
  async function texto(b: Blob): Promise<string> {
    return new TextDecoder().decode(await b.arrayBuffer());
  }

  it('con y sin prefijo produce los mismos bytes', async () => {
    expect(await texto(base64ABlob(PDF, 'application/pdf'))).toBe('%PDF-1.4');
    expect(await texto(base64ABlob(`data:application/pdf;base64,${PDF}`, 'application/pdf'))).toBe('%PDF-1.4');
  });

  it('respeta el tipo y cae a octet-stream', () => {
    expect(base64ABlob(PDF, 'application/pdf').type).toBe('application/pdf');
    expect(base64ABlob(PDF, null).type).toBe('application/octet-stream');
  });
});
