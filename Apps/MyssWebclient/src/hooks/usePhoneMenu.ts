import { useEffect, useState, type RefObject } from "react";
import { useLocation } from "react-router";

/**
 * Open state of the phone menu, which replaces the page while open. It
 * closes when it stops being available (the window widens, the user leaves
 * the dashboard) and when the route changes. Escape closes it and returns
 * focus to the menu button; focus lost with the panel moves to `main`.
 * `buttonRef` is the menu button's wrapper, since BCDS Button's props do
 * not accept a ref.
 */
export function usePhoneMenu(
  isAvailable: boolean,
  buttonRef: RefObject<HTMLElement | null>,
  mainRef: RefObject<HTMLElement | null>,
) {
  const { pathname } = useLocation();
  const [openedOn, setOpenedOn] = useState<string | null>(null);

  if (openedOn !== null && (!isAvailable || openedOn !== pathname)) {
    setOpenedOn(null);
  }
  const isOpen = openedOn !== null;

  useEffect(() => {
    if (!isOpen) return;
    const main = mainRef.current;
    window.scrollTo(0, 0);
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setOpenedOn(null);
      buttonRef.current?.querySelector("button")?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("keydown", closeOnEscape);
      if (document.activeElement === document.body) main?.focus();
    };
  }, [isOpen, buttonRef, mainRef]);

  return {
    isOpen,
    toggle: () => setOpenedOn(isOpen ? null : pathname),
    close: () => setOpenedOn(null),
  };
}
