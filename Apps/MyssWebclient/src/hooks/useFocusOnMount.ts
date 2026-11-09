import { useEffect, useRef } from "react";

/**
 * Moves focus to a heading when it appears, so a screen-reader user is told
 * what happened rather than being left at the button they pressed.
 */
export function useFocusOnMount() {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    headingRef.current?.focus();
  }, []);
  return headingRef;
}
