import { redirect } from "next/navigation";

/** There is no public landing page for staff tooling; go straight to the sign-in screen. */
export default function AdminHomePage() {
  redirect("/login");
}
