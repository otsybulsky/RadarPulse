import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  ProductHandlerOutput,
  ProductRunDetail,
} from '../../product/product-api.models';
import { ProductRequestState } from '../../product/product-api-state';

@Component({
  selector: 'app-operator-run-handlers-tab',
  imports: [FormsModule],
  templateUrl: './operator-run-handlers-tab.component.html',
})
export class OperatorRunHandlersTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;
  @Input() handlerOutput: ProductRequestState<ProductHandlerOutput> | null = null;
  @Input() handlerOutputLoading = false;
  @Input() handlerLookupValidationMessage = '';
  @Input() handlerLookupDisabled = false;
  @Input() handlerSourceId = 0;
  @Input() handlerFieldName = '';

  @Output() handlerSourceIdChange = new EventEmitter<number>();
  @Output() handlerFieldNameChange = new EventEmitter<string>();
  @Output() loadHandlerOutput = new EventEmitter<void>();
}
