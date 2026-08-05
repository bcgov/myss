import { Header, Footer } from "@bcgov/design-system-react-components";
import { Navigate, Route, Routes } from "react-router";

import "./App.css";
import FormsTechDemo from "@/pages/FormsTechDemo";
import SubmissionView from "@/pages/SubmissionView";
import TechDemos from "@/pages/TechDemos";

function App() {
  return (
    <>
      <Header title="Technical DEMO - My Self Serve" />
      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/techdemos" replace />} />
          <Route path="/techdemos" element={<TechDemos />} />
          <Route path="/techdemos/forms" element={<FormsTechDemo />} />
          <Route
            path="/techdemos/forms/submissions/:id"
            element={<SubmissionView />}
          />
        </Routes>
      </main>
      <Footer hideAcknowledgement />
    </>
  );
}

export default App;
