import { describe, expect, it } from 'vitest'
import { parseQuantity, quantity } from './quantity'
describe('quantidades de estoque sem perda de precisão', () => {
  it('converte PT-BR preservando seis decimais', () => expect(parseQuantity('123.456.789.012,123456')).toBe('123456789012.123456'))
  it('formata sem conversão para ponto flutuante', () => expect(quantity('123456789012.123456')).toBe('123.456.789.012,123456'))
  it('rejeita escala inválida sem truncar', () => expect(() => parseQuantity('1,001', 2)).toThrow('não foi arredondada'))
  it('aceita zeros extras sem mudar valor', () => expect(parseQuantity('001,2000', 1)).toBe('1.2'))
  it.each(['-1', '1.2', '1e3', '1,2,3', '1.23.456', 'NaN', '', '0'])('rejeita entrada inválida %s', value => expect(() => parseQuantity(value)).toThrow())
  it('permite mínimo zero', () => expect(parseQuantity('0', 0, false)).toBe('0'))
})
