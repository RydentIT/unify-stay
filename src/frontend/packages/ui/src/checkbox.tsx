import { type InputHTMLAttributes, useId } from "react";

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "type"> {
  label: string;
  errors?: string[];
}

/** Labelled checkbox with its error messages wired up for assistive technology. */
export function Checkbox({ label, errors = [], ...rest }: CheckboxProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hasErrors = errors.length > 0;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="flex items-center gap-2 text-sm">
        <input
          {...rest}
          id={id}
          type="checkbox"
          aria-invalid={hasErrors}
          aria-describedby={hasErrors ? errorId : undefined}
        />
        {label}
      </label>
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
