/**
 * mammoth ships browser and Node entry points; only the Node one is typed, and it pulls in `fs`,
 * which Vite cannot bundle. The browser build is the same API, so it borrows the same types.
 */
declare module 'mammoth/mammoth.browser' {
  import mammoth from 'mammoth'
  export = mammoth
}
