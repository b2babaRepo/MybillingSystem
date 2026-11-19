import { shrinkPhoto } from './images'

/** A bill file ready to send: a PDF as it is (it must be under 1.5 MB), a photo shrunk to a readable size. */
export async function billFile(file: File, tooBig: string): Promise<string> {
  if (file.type !== 'application/pdf') return shrinkPhoto(file, 1600, 1_200_000, false)
  if (file.size > 1_500_000) throw new Error(tooBig)
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(String(reader.result))
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}
