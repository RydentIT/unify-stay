export interface FormErrorProps {
  message?: string;
}

/** Inline field error. Rendered only when there is something to say. */
export function FormError({ message }: FormErrorProps) {
  if (!message) {
    return null;
  }

  return (
    <p role="alert" className="text-sm text-red-700">
      {message}
    </p>
  );
}
