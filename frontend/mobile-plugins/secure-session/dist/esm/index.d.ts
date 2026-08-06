export interface SecureSessionPlugin {
  set(options: { value: string }): Promise<void>;
  get(): Promise<{ value?: string }>;
  clear(): Promise<void>;
}
export declare const SecureSession: SecureSessionPlugin;
