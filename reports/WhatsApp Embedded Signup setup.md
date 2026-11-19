# WhatsApp Embedded Signup: what to set up at Meta

The code is in place (Settings → "Your WhatsApp Business number"). The **Connect WhatsApp Business** button stays hidden and shows "available soon" until the Meta app below exists and the server has its settings.

## 1. Meta side (once, for Grocery Khata)

1. **Meta Business account** for Grocery Khata at business.facebook.com, with **business verification** done (GST/PAN documents, website, domain).
2. **Meta app** at developers.facebook.com → Create app → type *Business* → add the **WhatsApp** product.
3. **Become a Tech Provider:** in the app's WhatsApp settings, start "Tech Provider" onboarding. App review asks for:
   - `whatsapp_business_management` and `whatsapp_business_messaging` (advanced access)
   - a short screen recording of a shop pressing Connect and sending the test message
   - privacy policy and terms URLs on the Grocery Khata site
4. **Facebook Login for Business** product → *Configurations* → create one for **WhatsApp Embedded Signup**. Copy its **Configuration ID**.
5. **Allowed domains:** in Facebook Login settings, add the app's address (e.g. `https://app.<your-domain>`) to *Allowed domains for the JavaScript SDK* and *Valid OAuth redirect URIs*. The SDK only works over **https**.
6. **Webhook:** WhatsApp → Configuration → callback `https://<api-domain>/api/v1/whatsapp/webhook`, the same verify token as today, subscribe to `messages`. (Already used for WhatsApp orders.)
7. **Payment method** for messages: either each shop adds its own card in Meta, or Grocery Khata pays as a Tech Provider and recovers the cost through plans.

## 2. Server settings

| Setting | What |
|---|---|
| `Whatsapp:AppId` | The Meta app's App ID |
| `Whatsapp:AppSecret` | The app secret (also checks webhook signatures) |
| `Whatsapp:EmbeddedSignupConfigId` | The Configuration ID from step 4 |
| `Whatsapp:GraphVersion` | Optional, default `v23.0` |
| `DataProtection:KeysPath` | **Required in production:** a folder on a kept volume (e.g. `/var/lib/grocery-khata/keys`). The shops' WhatsApp tokens are encrypted with keys kept here; without it, a restart or new container makes every shop reconnect. Back this folder up with the database. |

Keep `AppSecret` in user-secrets or an environment variable, never in a committed file.

## 3. Web server

`deploy/nginx-grocery-khata.conf` now lets the page load Meta's SDK and open its popup (`connect.facebook.net`, `*.facebook.com` in script-src, connect-src and frame-src). Reload nginx after deploying.

## 4. What a shop does

Settings → **Connect WhatsApp Business** → signs in with Facebook → picks or creates a WhatsApp Business account → picks a number (a new number, or one not in use in the WhatsApp app) → Meta sends a code by SMS or call → done. Then **Send a test message** sends Meta's `hello_world` template from the shop's number to the owner's phone.

## 5. What this first version does and does not do

- Does: connect, show number / business name / quality rating, test message, change number, disconnect; token encrypted, never sent to the browser; a number can belong to only one shop; messages to a shop's number are not treated as household orders.
- Not yet: sending bills, reminders and statements from the shop's number (needs per-shop message templates approved by Meta: "bill", "payment reminder", "statement"), and a shop inbox for customers' replies ("send my last invoice"). These are the next steps once a real shop is connected.
