// Shop photos of items, and photos of lists for the AI to read. The phone shrinks a photo before upload, so
// nothing heavy goes over the network or into the database, and no image library is needed anywhere.

/** Where a product's photo is served. The photo's time is in the URL, so a new photo is a new URL. */
export function imageUrl(shopId: string, productId: string, imageAt: string | null | undefined) {
  return imageAt ? `/api/v1/images/${shopId}/${productId}?v=${Date.parse(imageAt)}` : undefined
}

/**
 * A camera or gallery photo as a WebP data URL, at most `size` px on its longer side and about `maxBytes`:
 * quality steps down until it fits. `square` crops the centre (item photos, white behind, like shopping apps);
 * otherwise the whole photo is kept (a list or screenshot for the AI to read).
 */
export async function shrinkPhoto(file: File, size = 512, maxBytes = 140_000, square = true): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const side = Math.min(bitmap.width, bitmap.height)
  const scale = Math.min(1, size / (square ? side : Math.max(bitmap.width, bitmap.height)))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round((square ? side : bitmap.width) * scale)
  canvas.height = Math.round((square ? side : bitmap.height) * scale)
  const context = canvas.getContext('2d')!
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, canvas.width, canvas.height)
  if (square) {
    context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, canvas.width, canvas.height)
  } else {
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  }
  bitmap.close()
  for (const quality of [0.82, 0.7, 0.55, 0.4]) {
    const url = canvas.toDataURL('image/webp', quality)
    if (url.length * 0.75 <= maxBytes) return url
  }
  return canvas.toDataURL('image/jpeg', 0.5)
}
