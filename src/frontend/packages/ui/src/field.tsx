import { type InputHTMLAttributes, useId } from "react";

export interface FieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  /** Validation messages for this field, as returned in a problem document. */
  errors?: string[];
}

/**
 * A labelled input with its error messages wired up for assistive technology. The one piece of
 * real behaviour here is the aria-invalid / aria-describedby pairing, which is easy to get
 * wrong per-form and worth centralising even before the design system exists.
 */
export function Field({ label, errors = [], ...rest }: FieldProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hasErrors = errors.length > 0;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm">
        {label}
      </label>
      <input
        {...rest}
        id={id}
        aria-invalid={hasErrors}
        aria-describedby={hasErrors ? errorId : undefined}
        className="rounded border border-gray-400 px-2 py-1 text-sm"
      />
      {hasErrors ? (
        <ul id={errorId} className="text-sm text-red-700">
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
