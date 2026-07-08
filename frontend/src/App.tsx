import { Helmet } from 'react-helmet-async'

function App() {
  return (
    <>
      <Helmet>
        <title>Watodoo</title>
      </Helmet>
      <main className="flex min-h-screen flex-col items-center justify-center gap-2 bg-white text-slate-900 dark:bg-slate-900 dark:text-white">
        <h1 className="text-3xl font-semibold">Watodoo</h1>
        <p className="text-slate-500 dark:text-slate-400">
          En quelques clics, découvre quoi faire ce soir.
        </p>
      </main>
    </>
  )
}

export default App
