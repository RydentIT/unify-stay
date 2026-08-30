"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { loginSchema, type LoginFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Checkbox, Field } from "@unify/ui";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { GoogleAuthButton } from "@/components/GoogleAuthButton";
import { api } from "@/lib/api";
import { toFormFailure, type FormFailure } from "@/lib/problem";
import { writeSession } from "@/lib/session";

export default function LoginPage() {
  const router = useRouter();
  const [failure, setFailure] = useState<FormFailure | null>(null);

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "", rememberMe: false },
  });

  async function onSubmit(values: LoginFormValues) {
    setFailure(null);

    try {
      const result = await api.auth.login({
        email: values.email,
        password: values.password,
        rememberMe: values.rememberMe ?? false,
      });

      writeSession({
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        mustChangePassword: result.mustChangePassword,
        mustCompleteProfile: result.mustCompleteProfile,
      });

      // LOG-013: a limited-scope token leads to exactly one place. Email/password accounts
      // never owe a phone number, but the check is harmless to keep in step with GoogleAuthButton.
      router.replace(
        result.mustChangePassword
          ? "/change-password-required"
          : result.mustCompleteProfile
            ? "/complete-profile"
            : "/profile",
      );
    } catch (error) {
      setFailure(toFormFailure(error));
    }
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Log in</h1>

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
        {/* BR-LOG-005: this lengthens the refresh token only. */}
        <Checkbox label="Remember me" {...register("rememberMe")} />

        <Button type="submit" pending={isSubmitting}>
          Log in
        </Button>
      </form>

      {failure ? (
        <div className="flex flex-col gap-2">
          <Alert tone="error">{failure.message}</Alert>
          {/* LOG-004 is the one failure the user can act on directly. */}
          {failure.code === "auth.login.email_not_verified" ? (
            <p className="text-sm">
              <Link href="/verify-email">Resend the verification email</Link>
            </p>
          ) : null}
        </div>
      ) : null}

      <div className="flex flex-col gap-2 border-t pt-3">
        <GoogleAuthButton
          mode="login"
          onFailure={setFailure}
          rememberMe={getValues("rememberMe") ?? false}
        />
      </div>

      <div className="flex flex-col gap-1 text-sm">
        <Link href="/forgot-password">Forgot your password?</Link>
        <Link href="/register">Create an account</Link>
      </div>
    </section>
  );
}
