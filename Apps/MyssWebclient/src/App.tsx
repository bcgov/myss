import { Header, Footer } from "@bcgov/design-system-react-components";

import "./App.css";
import useDemo from "@/hooks/useDemo";

function App() {
  const { data, error, isPending } = useDemo();

  return (
    <>
      <Header title="Technical DEMO - My Self Serve" />
      <main>
        <h1>Tech demo - My Self Serve</h1>
        {isPending && <p>Loading…</p>}
        {error && <p>An error has occurred: {error.message}</p>}
        {data && (
          <dl>
            <dt>Foo</dt>
            <dd>{data.foo}</dd>
            <dt>Bar</dt>
            <dd>{data.bar}</dd>
          </dl>
        )}
      </main>
      <Footer hideAcknowledgement />
    </>
  );
}

export default App;
