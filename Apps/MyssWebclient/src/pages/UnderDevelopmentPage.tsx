// Stand-in for a dashboard section whose feature is not built yet (MYSS-194,
// AC3). Rendered inside the dashboard layout, so the menu stays in reach.
export default function UnderDevelopmentPage({ title }: { title: string }) {
  return (
    <>
      <h1>{title}</h1>
      <p>This feature is under development. Please check back later.</p>
    </>
  );
}
