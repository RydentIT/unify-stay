"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema, type LoginFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Field } from "@unify/ui";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { writeSession } from "@/lib/session";

/**
 * Staff sign-in.
 *
 * Deliberately offers NO Google button and NO registration link: admin accounts are
 * provisioned internally, and LOG-016 blocks Admin/Staff from signing in through Google at all,
 * so showing the button would only produce a confusing rejection.
 */
export default function AdminLoginPage() {
  const router = useRouter();
  const [failure, setFailure] = useState<FormFailure | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "", rememberMe: false },
  });

  async function onSubmit(values: LoginFormValues) {
    setFailure(null);

    try {
      const result = await api.auth.login({ email: values.email, password: values.password });

      writeSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        mustChangePassword: result.mustChangePassword,
      });

      router.replace(result.mustChangePassword ? "/change-password-required" : "/dashboard");
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Staff sign in</h1>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="Email"
          type="email"
          autoComplete="email"
          errors={errors.email?.message ? [errors.email.message] : []}
          {...register("email")}
        />
        <Field
          label="Password"
          type="password"
          autoComplete="current-password"
          errors={errors.password?.message ? [errors.password.message] : []}
          {...register("password")}
        />
        <Button type="submit" pending={isSubmitting}>
          Sign in
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}
    </section>
  );
}
