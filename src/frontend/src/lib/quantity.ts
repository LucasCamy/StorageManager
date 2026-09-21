/** PT-BR: dots group thousands; commas mark decimal places. Never round stock quantities. */
export function parseQuantity(input: string, scale = 6, positive = true): string {
  const value = input.trim()
  if (!/^(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d+)?$/.test(value)) throw new Error('Informe uma quantidade válida. Use vírgula para decimais, por exemplo: 1,5.')
  const [rawWhole, rawFraction = ''] = value.replaceAll('.', '').split(',')
  const whole = rawWhole.replace(/^0+(?=\d)/, '')
  const fraction = rawFraction.replace(/0+$/, '')
  if (fraction.length > scale) throw new Error(`Este produto aceita no máximo ${scale} casa(s) decimal(is). A quantidade não foi arredondada.`)
  if (whole.length > 12) throw new Error('A quantidade excede o limite de 12 dígitos inteiros.')
  if (positive && whole === '0' && !fraction) throw new Error('A quantidade precisa ser maior que zero.')
  return fraction ? `${whole}.${fraction}` : whole
}
export function quantity(value: string): string {
  const [whole, fraction = ''] = value.split('.')
  const suffix = fraction.replace(/0+$/, '')
  return `${whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}${suffix ? `,${suffix}` : ''}`
}
export function dateTime(value: string): string {
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}
