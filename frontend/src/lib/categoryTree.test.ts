import { describe, expect, it } from 'vitest'
import { buildMenu, categoryOf, kindOf } from './categoryTree'
import type { Product } from './types'

const item = (name: string, category: string | null, extra: Partial<Product> = {}): Product =>
  ({ id: name, name, category, unit: 'kg', sellingPrice: 100, costPrice: null, stock: 5, reorderLevel: null, isLow: false, ...extra }) as Product

describe('categoryTree', () => {
  it('splits the merged atta, rice and dal group by the name', () => {
    expect(categoryOf(item('तूर दाल', 'आटा, चावल, दाल')).key).toBe('dal')
    expect(categoryOf(item('Basmati rice', 'आटा, चावल, दाल')).key).toBe('rice')
    expect(categoryOf(item('Aashirvaad Atta', 'आटा, चावल, दाल')).key).toBe('atta')
    expect(categoryOf(item('Maggi Masala Noodles', 'इंस्टेंट, नाश्ता')).key).toBe('noodles')
    expect(categoryOf(item('Tata Tea Gold', 'चाय, कॉफ़ी, दूध')).key).toBe('tea')
    expect(categoryOf(item('Nescafe Classic', 'चाय, कॉफ़ी, दूध')).key).toBe('coffee')
  })

  it('places a typed item by its name words, else under Other', () => {
    expect(categoryOf(item('Kaju 250 g', null)).key).toBe('dryfruit')
    expect(categoryOf(item('Moong dal loose', 'My stuff')).key).toBe('dal')
    expect(categoryOf(item('Umbrella', null)).key).toBe('other')
    expect(categoryOf(item('Sandal soap', null)).key).not.toBe('dal')
  })

  it('finds the kind chip inside a category', () => {
    const dal = categoryOf(item('Toor dal', 'आटा, चावल, दाल'))
    expect(kindOf(item('Toor dal', null), dal)).toBe('toor')
    expect(kindOf(item('मूंग दाल धुली', null), dal)).toBe('moong')
  })

  it('shows only tiles with items, most sold first', () => {
    const menu = buildMenu([item('Chana dal', 'आटा, चावल, दाल'), item('Toor dal', 'आटा, चावल, दाल'), item('Jeera', 'मसाले')], new Map([['Toor dal', 1]]))
    expect(menu.map((d) => d.department.key)).toEqual(['staples', 'masala'])
    expect(menu[0].categories.map((c) => c.category.key)).toEqual(['dal'])
    expect(menu[0].categories[0].items.map((p) => p.name)).toEqual(['Toor dal', 'Chana dal'])
  })

  it('falls back to the shop’s own categories for another trade', () => {
    const menu = buildMenu([item('Shirt', 'Men · Shirts'), item('Saree', 'Women · Sarees')], new Map())
    expect(menu).toHaveLength(1)
    expect(menu[0].categories.map((c) => c.category.en)).toEqual(['Men · Shirts', 'Women · Sarees'])
  })
})
