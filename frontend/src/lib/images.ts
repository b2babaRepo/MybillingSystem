// Shop photos of items. The phone shrinks a photo to a small WebP square before upload, so nothing heavy
// goes over the network or into the database, and no image library is needed anywhere.

/** Where a product's photo is served. The photo's time is in the URL, so a new photo is a new URL. */
export function imageUrl(shopId: string, productId: string, imageAt: string | null | undefined) {
  return imageAt ? `/api/v1/images/${shopId}/${productId}?v=${Date.parse(imageAt)}` : undefined
}

const SIZE = 512
const MAX_BYTES = 140_000

/**
 * A camera or gallery photo as a centred square WebP data URL, at most 512 px and ~140 KB: quality steps down
 * until it fits. White behind it, like the product photos in shopping apps.
 */
export async function shrinkPhoto(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const side = Math.min(bitmap.width, bitmap.height)
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = Math.min(SIZE, side)
  const context = canvas.getContext('2d')!
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, canvas.width, canvas.height)
  context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  for (const quality of [0.82, 0.7, 0.55, 0.4]) {
    const url = canvas.toDataURL('image/webp', quality)
    if (url.length * 0.75 <= MAX_BYTES) return url
  }
  return canvas.toDataURL('image/jpeg', 0.5)
}
