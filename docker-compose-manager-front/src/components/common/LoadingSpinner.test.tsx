import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoadingSpinner } from './LoadingSpinner';

describe('LoadingSpinner', () => {
  it('renders without crashing', () => {
    render(<LoadingSpinner />);
    const spinner = screen.getByRole('status');
    expect(spinner).toBeInTheDocument();
  });

  it('displays text when provided', () => {
    render(<LoadingSpinner text="Loading data..." />);
    expect(screen.getByText('Loading data...')).toBeInTheDocument();
  });

  it('renders with different sizes', () => {
    const { rerender } = render(<LoadingSpinner size="sm" />);
    let spinner = screen.getByRole('status').querySelector('svg');
    expect(spinner).toHaveClass('w-4');

    rerender(<LoadingSpinner size="md" />);
    spinner = screen.getByRole('status').querySelector('svg');
    expect(spinner).toHaveClass('w-8');

    rerender(<LoadingSpinner size="lg" />);
    spinner = screen.getByRole('status').querySelector('svg');
    expect(spinner).toHaveClass('w-12');
  });
});
