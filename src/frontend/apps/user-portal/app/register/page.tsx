"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { registerSchema, type RegisterFormValues } from "@unify/api-client/schemas";
import { Alert, Button, Checkbox, Field } from "@unify/ui";
import Link from "next/link";
import { useState } from "react";
import { useForm } from "react-hook-form";

import { GoogleAuthButton } from "@/components/GoogleAuthButton";
import { api } from "@/lib/api";
import { applyFieldErrors, toFormFailure, type FormFailure } from "@/lib/problem";

const FIELDS = [
  "firstName",
  "lastName",
  "email",
  "password",
  "confirmPassword",
  "contactNumber",
  "acceptedTerms",
] as const;

export default function RegisterPage() {
  const [failure, setFailure] = useState<FormFailure | null>(null);
  const [registeredEmail, setRegisteredEmail] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      firstName: "",
      lastName: "",
      email: "",
      password: "",
      confirmPassword: "",
      contactNumber: "",
      acceptedTerms: false,
    },
  });

  async function onSubmit(values: RegisterFormValues) {
    setFailure(null);
    setRegisteredEmail(null);

    try {
      // confirmPassword is a UI-only field and is deliberately not sent.
      const result = await api.auth.register({
        firstName: values.firstName,
        lastName: values.lastName,
        email: values.email,
        password: values.password,
        contactNumber: values.contactNumber,
        acceptedTerms: values.acceptedTerms,
      });

      setRegisteredEmail(result.email);
    } catch (error) {
      const formFailure = toFormFailure(error);
      setFailure(formFailure);
      applyFieldErrors(formFailure, setError as never, FIELDS);
    }
  }

  // EVR-001: registration always leads to the "check your email" state.
  if (registeredEmail) {
    return (
      <section className="flex flex-col gap-3">
        <h1 className="text-xl font-semibold">Check your email</h1>
        <Alert>
          We have sent a verification link to <strong>{registeredEmail}</strong>. Confirm it to
          finish setting up your account.
        </Alert>
        <p className="text-sm">
          Nothing arrived? <Link href="/verify-email">Request a new link</Link>.
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Create an account</h1>

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-3">
        <Field
          label="First name"
          autoComplete="given-name"
          errors={errors.firstName?.message ? [errors.firstName.message] : []}
          {...register("firstName")}
        />
        <Field
          label="Last name"
          autoComplete="family-name"
          errors={errors.lastName?.message ? [errors.lastName.message] : []}
          {...register("lastName")}
        />
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
          autoComplete="new-password"
          errors={errors.password?.message ? [errors.password.message] : []}
          {...register("password")}
        />
        <Field
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          errors={errors.confirmPassword?.message ? [errors.confirmPassword.message] : []}
          {...register("confirmPassword")}
        />
        <Field
          label="Phone number"
          type="tel"
          autoComplete="tel"
          errors={errors.contactNumber?.message ? [errors.contactNumber.message] : []}
          {...register("contactNumber")}
        />
        <Checkbox
          label="I accept the terms of service"
          errors={errors.acceptedTerms?.message ? [errors.acceptedTerms.message] : []}
          {...register("acceptedTerms")}
        />

        <Button type="submit" pending={isSubmitting}>
          Register
        </Button>
      </form>

      {failure ? <Alert tone="error">{failure.message}</Alert> : null}

      <div className="flex flex-col gap-2 border-t pt-3">
        <p className="text-sm">Or sign up with Google:</p>
        <GoogleAuthButton mode="register" onFailure={setFailure} />
      </div>

      <p className="text-sm">
        Already have an account? <Link href="/login">Log in</Link>
      </p>
    </section>
  );
}
