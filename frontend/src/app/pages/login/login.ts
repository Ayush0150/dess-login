import { Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
@Component({
  imports: [ReactiveFormsModule],
  selector: 'app-login',
  styleUrl: './login.css',
  templateUrl: './login.html',
})
export class Login {
  showPassword = false;
  username = new FormControl('', Validators.required);
  password = new FormControl('', Validators.required);

  loginForm = new FormGroup({
    username: this.username,
    password: this.password,
  });

  onSubmit() {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    console.log('Login form is valid');
  }
}
